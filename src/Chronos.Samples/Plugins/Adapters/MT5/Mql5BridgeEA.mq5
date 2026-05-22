//+------------------------------------------------------------------+
//|                                      Chronos MT5 Bridge EA.mq5   |
//|                                   Copyright 2026, Chronos Co.    |
//|                                             https://chronos.com  |
//+------------------------------------------------------------------+
#property copyright "Copyright 2026, Chronos Co."
#property link      "https://chronos.com"
#property version   "2.01"
#property strict

#include <Trade/Trade.mqh>
#include <Trade/PositionInfo.mqh>
#include <Trade/OrderInfo.mqh>
#include <WinAPI/winsock.mqh>

#define BUFFER_SIZE 65536
uchar receiveBuffer[BUFFER_SIZE];
int   readPos  = 0;     // next byte to read
int   writePos = 0;     // next free position (wraps circular)

input int    BridgePort = 5555;
input int    MaxClients = 1;
input bool   DebugLog   = true;

CTrade        Trade;
CPositionInfo PositionInfo;
COrderInfo    OrderInfo;

int           ServerSocket = INVALID_SOCKET;
int           ClientSocket = INVALID_SOCKET;
bool          IsConnected = false;

// ---- Multi‑symbol tick streaming (optimised) ----
#define MAX_SYMBOLS 100
string SubscribedSymbols[MAX_SYMBOLS];
long   LastTickTimeMs[MAX_SYMBOLS];  // store only time_msc (saves memory & copy)
int    SubCount = 0;
string HostSymbol;                   // chart symbol
int    HostSymbolIndex = -1;         // cached index for O(1) host check

// Helper union for double -> byte array conversion (avoids deprecated pointers)
union DoubleBytes { double d; uchar c[8]; };

//=== ADDED: Track previous state for execution reports ==================
int   PreviousPositionsTotal = 0;
int   PreviousOrdersTotal    = 0;
//====================================================================

// Forward declaration of local helpers
char GetChar(string text, int index);

//+------------------------------------------------------------------+
//| Expert initialization                                             |
//+------------------------------------------------------------------+
int OnInit()
{
   Trade.SetExpertMagicNumber(0);

   WSADATA wsaData;
   if(WSAStartup(0x202, wsaData) != 0)
   {
      Print("WSAStartup failed. Error ", GetLastError());
      return INIT_FAILED;
   }

   ServerSocket = socket(AF_INET, SOCK_STREAM, 0);
   if(ServerSocket == INVALID_SOCKET)
   {
      Print("Socket creation failed. Error ", WSAGetLastError());
      WSACleanup();
      return INIT_FAILED;
   }

   // Set non‑blocking so recv never blocks the timer – use uint for ioctlsocket
   uint mode = 1;
   ioctlsocket(ServerSocket, FIONBIO, mode);

   sockaddr_in addr;
   addr.sin_family = AF_INET;
   addr.sin_port   = htons((ushort)BridgePort);    // explicit ushort cast, no bitwise mask needed
   addr.sin_addr   = INADDR_ANY;

   if(bind(ServerSocket, addr, sizeof(sockaddr_in)) == SOCKET_ERROR)
   {
      Print("Socket bind failed. Error ", WSAGetLastError());
      closesocket(ServerSocket);
      WSACleanup();
      return INIT_FAILED;
   }

   if(listen(ServerSocket, MaxClients) == SOCKET_ERROR)
   {
      Print("Socket listen failed. Error ", WSAGetLastError());
      closesocket(ServerSocket);
      WSACleanup();
      return INIT_FAILED;
   }

   HostSymbol = _Symbol;
   Print("Chronos Bridge EA listening on port ", BridgePort);
   EventSetMillisecondTimer(10);   // poll every 10 ms
   return INIT_SUCCEEDED;
}

//+------------------------------------------------------------------+
//| Expert deinitialization                                           |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
{
   EventKillTimer();
   if(ClientSocket != INVALID_SOCKET) { closesocket(ClientSocket); ClientSocket = INVALID_SOCKET; }
   if(ServerSocket != INVALID_SOCKET) { closesocket(ServerSocket); ServerSocket = INVALID_SOCKET; }
   WSACleanup();
   IsConnected = false;
}

//+------------------------------------------------------------------+
//| Tick event – only for the host symbol (immediate)                 |
//+------------------------------------------------------------------+
void OnTick()
{
   if(ClientSocket == INVALID_SOCKET || HostSymbolIndex < 0) return;

   MqlTick tick;
   if(SymbolInfoTick(HostSymbol, tick))
   {
      // Send every tick – use time_msc for precise duplicate detection
      if(tick.time_msc > LastTickTimeMs[HostSymbolIndex])
      {
         LastTickTimeMs[HostSymbolIndex] = tick.time_msc;
         SendTick(HostSymbol, tick);
      }
   }
}

//+------------------------------------------------------------------+
//| Timer – accept clients, read messages, poll non‑host symbols      |
//+------------------------------------------------------------------+
void OnTimer()
{
   // Accept new client if none
   if(ClientSocket == INVALID_SOCKET && ServerSocket != INVALID_SOCKET)
   {
      sockaddr_in clientAddr;
      int addrLen = sizeof(sockaddr_in);
      ClientSocket = accept(ServerSocket, clientAddr, addrLen);
      if(ClientSocket != INVALID_SOCKET)
      {
         IsConnected = true;
         // Set client socket non‑blocking too
         uint mode = 1;
         ioctlsocket(ClientSocket, FIONBIO, mode);
         Print("Client connected.");
         readPos = writePos = 0;   // reset circular buffer
      }
   }

   if(ClientSocket != INVALID_SOCKET)
   {
      // ---- Read all available data directly into the circular buffer ----
      int space;
      while(true)
      {
         // Determine contiguous space available from writePos to end or wrap
         if(writePos >= readPos)
         {
            space = BUFFER_SIZE - writePos;
            if(readPos == 0) space--;  // prevent full buffer, keep one byte gap
         }
         else
         {
            space = readPos - writePos - 1;
         }
         if(space <= 0) { Print("Buffer full, clearing."); readPos = writePos = 0; break; }

         int bytesRead = recv(ClientSocket, receiveBuffer, space, 0);
         if(bytesRead > 0)
         {
            writePos = (writePos + bytesRead) % BUFFER_SIZE;
         }
         else
         {
            // Error or no data
            if(bytesRead == SOCKET_ERROR && WSAGetLastError() != WSAEWOULDBLOCK)
            {
               Print("Client error ", WSAGetLastError());
               closesocket(ClientSocket);
               ClientSocket = INVALID_SOCKET;
               IsConnected = false;
               readPos = writePos = 0;
               return;
            }
            else if(bytesRead == 0)
            {
               Print("Client disconnected.");
               closesocket(ClientSocket);
               ClientSocket = INVALID_SOCKET;
               IsConnected = false;
               readPos = writePos = 0;
               return;
            }
            break;  // no more data now
         }
      }

      // ---- Process complete messages in buffer (no shifting!) ----
      while(true)
      {
         // Need at least 4 bytes for length prefix
         int avail = (writePos >= readPos) ? (writePos - readPos) : (BUFFER_SIZE - readPos + writePos);
         if(avail < 4) break;

         // Read 4‑byte length (little‑endian) without moving readPos
         uchar lenBuf[4];
         for(int i=0; i<4; i++)
            lenBuf[i] = receiveBuffer[(readPos + i) % BUFFER_SIZE];
         int msgLen = lenBuf[0] | (lenBuf[1] << 8) | (lenBuf[2] << 16) | (lenBuf[3] << 24);

         if(msgLen <= 0 || msgLen > 1024*1024)
         {
            // Corrupt – skip one byte
            readPos = (readPos + 1) % BUFFER_SIZE;
            continue;
         }

         // Check if whole message is available
         int totalNeeded = 4 + msgLen;
         if(avail < totalNeeded) break;

         // Extract message body (avoid copying if possible, but we need contiguous array)
         uchar msgBody[];
         ArrayResize(msgBody, msgLen, 0);
         for(int i=0; i<msgLen; i++)
            msgBody[i] = receiveBuffer[(readPos + 4 + i) % BUFFER_SIZE];

         // Advance read position
         readPos = (readPos + totalNeeded) % BUFFER_SIZE;

         // Parse and process
         string json = CharArrayToString(msgBody, 0, msgLen);
         ProcessMessage(json);
      }

      // ---- Poll non‑host symbols for ticks (unchanged logic, now uses time_msc) ----
      if(IsConnected)
      {
         for(int i=0; i<SubCount; i++)
         {
            if(i == HostSymbolIndex) continue;  // handled by OnTick
            MqlTick tick;
            if(SymbolInfoTick(SubscribedSymbols[i], tick))
            {
               if(tick.time_msc > LastTickTimeMs[i])
               {
                  LastTickTimeMs[i] = tick.time_msc;
                  SendTick(SubscribedSymbols[i], tick);
               }
            }
         }

         //=== ADDED: Send execution updates ================================
         CheckExecutionUpdates();
         //==================================================================
      }
   }
}

//=== ADDED: Check for position/order changes and send execution reports ===
void CheckExecutionUpdates()
{
   int posTotal = PositionsTotal();
   int ordTotal = OrdersTotal();

   // Compare with previous totals (simple approach; full implementation would cache state)
   if(posTotal != PreviousPositionsTotal || ordTotal != PreviousOrdersTotal)
   {
      PreviousPositionsTotal = posTotal;
      PreviousOrdersTotal    = ordTotal;

      // Send current state of all positions (simplified: each position as Filled execution)
      for(int i = 0; i < posTotal; i++)
      {
         ulong ticket = PositionGetTicket(i);
         if(PositionSelectByTicket(ticket))
         {
            string json = StringFormat(
               "{\"requestId\":null,\"command\":\"execution\",\"payload\":{\"ticket\":%I64u,\"symbol\":\"%s\",\"type\":\"%s\",\"state\":\"Filled\",\"executedVolume\":%f,\"executedPrice\":%f,\"remainingVolume\":0.0,\"commission\":%f,\"realizedPnL\":%f,\"timestamp\":%I64d,\"comment\":\"\"}}",
               ticket,
               PositionGetString(POSITION_SYMBOL),
               MqlTypeToChronosType((int)PositionGetInteger(POSITION_TYPE)),
               PositionGetDouble(POSITION_VOLUME),
               PositionGetDouble(POSITION_PRICE_CURRENT),
               PositionGetDouble(POSITION_COMMISSION),
               PositionGetDouble(POSITION_PROFIT),
               TimeCurrent()
            );
            SendExecutionReport(json);
         }
      }
      // Also send pending orders (simplified)
      for(int i = 0; i < ordTotal; i++)
      {
         ulong ticket = OrderGetTicket(i);
         if(OrderSelect(ticket))
         {
            string json = StringFormat(
               "{\"requestId\":null,\"command\":\"execution\",\"payload\":{\"ticket\":%I64u,\"symbol\":\"%s\",\"type\":\"%s\",\"state\":\"New\",\"executedVolume\":0.0,\"executedPrice\":0.0,\"remainingVolume\":%f,\"commission\":0.0,\"realizedPnL\":0.0,\"timestamp\":%I64d,\"comment\":\"\"}}",
               ticket,
               OrderGetString(ORDER_SYMBOL),
               MqlTypeToChronosType((int)OrderGetInteger(ORDER_TYPE)),
               OrderGetDouble(ORDER_VOLUME_CURRENT),
               TimeCurrent()
            );
            SendExecutionReport(json);
         }
      }
   }
}

void SendExecutionReport(string json)
{
   uchar body[];
   StringToCharArray(json, body, 0, StringLen(json));
   int len = ArraySize(body);
   uchar packet[];
   ArrayResize(packet, 4 + len);
   IntToArrayLittleEndian(len, packet, 0);
   ArrayCopy(packet, body, 4, 0, len);
   send(ClientSocket, packet, 4 + len, 0);
}
//====================================================================

// ... (rest of the file remains unchanged: SubscribeSymbol, UnsubscribeSymbol, SendTick, ProcessMessage, etc.) ...

//+------------------------------------------------------------------+
//| Subscribe / unsubscribe helpers                                   |
//+------------------------------------------------------------------+
void SubscribeSymbol(string symbol)
{
   if(FindSymbolIndex(symbol) >= 0) return;
   if(SubCount >= MAX_SYMBOLS) return;

   SymbolSelect(symbol, true);
   SubscribedSymbols[SubCount] = symbol;
   LastTickTimeMs[SubCount] = 0;
   SubCount++;

   // Update host symbol cache if it matches
   if(symbol == HostSymbol) HostSymbolIndex = SubCount - 1;
}

void UnsubscribeSymbol(string symbol)
{
   int idx = FindSymbolIndex(symbol);
   if(idx < 0) return;

   SubscribedSymbols[idx] = SubscribedSymbols[SubCount-1];
   LastTickTimeMs[idx] = LastTickTimeMs[SubCount-1];
   SubCount--;

   // Update host symbol cache
   if(symbol == HostSymbol) HostSymbolIndex = -1;
   else if(HostSymbolIndex == SubCount) HostSymbolIndex = idx;  // moved
}

int FindSymbolIndex(string symbol)
{
   // Host symbol shortcut (O(1) for the most frequent case)
   if(HostSymbolIndex >= 0 && SubscribedSymbols[HostSymbolIndex] == symbol)
      return HostSymbolIndex;

   for(int i=0; i<SubCount; i++)
      if(SubscribedSymbols[i] == symbol)
         return i;
   return -1;
}

//+------------------------------------------------------------------+
//| Send a tick as an unsolicited event (single send call)            |
//+------------------------------------------------------------------+
void SendTick(string symbol, const MqlTick &tick)
{
   string json = StringFormat(
      "{\"requestId\":null,\"command\":\"tick\",\"payload\":{\"symbol\":\"%s\",\"tick\":{\"time\":%I64d,\"bid\":%f,\"ask\":%f,\"volume\":%f,\"isSynthetic\":false}}}",
      symbol, tick.time, tick.bid, tick.ask, tick.volume);

   uchar body[];
   StringToCharArray(json, body, 0, StringLen(json));
   int len = ArraySize(body);

   // Combine header + body into one buffer and send once
   uchar packet[];
   ArrayResize(packet, 4 + len);
   IntToArrayLittleEndian(len, packet, 0);
   ArrayCopy(packet, body, 4, 0, len);

   send(ClientSocket, packet, 4 + len, 0);
}

//+------------------------------------------------------------------+
//| Process a JSON message – optimized parsing                        |
//+------------------------------------------------------------------+
void ProcessMessage(string json)
{
   if(DebugLog) Print("Received: ", json);

   // --- Fast one‑pass extraction of top‑level keys ---
   string requestId, command, payload;
   if(!JsonGetTopFields(json, requestId, command, payload))
      return;   // malformed

   if(command == "subscribe")
   {
      string symbol = JsonGetSimple(payload, "symbol");
      SubscribeSymbol(symbol);
      SendResponse(requestId, "subscribe", "{\"success\":true}");
   }
   else if(command == "unsubscribe")
   {
      string symbol = JsonGetSimple(payload, "symbol");
      UnsubscribeSymbol(symbol);
      SendResponse(requestId, "unsubscribe", "{\"success\":true}");
   }
   else if(command == "fetchHistory")
   {
      string symbol   = JsonGetSimple(payload, "symbol");
      string startStr = JsonGetSimple(payload, "startTime");
      string endStr   = JsonGetSimple(payload, "endTime");
      datetime start  = StringToTime(startStr);
      datetime end    = StringToTime(endStr);

      MqlTick ticks[];
      int copied = CopyTicksRange(symbol, ticks, COPY_TICKS_ALL, (long)start*1000, (long)end*1000);
      if(copied <= 0)
      {
         SendResponse(requestId, "fetchHistory", "{\"success\":false,\"error\":\"No data\"}");
         return;
      }

      // Write binary file – use a temporary byte array and write in one shot
      string fileName = symbol + "_" + IntegerToString(start) + "_" + IntegerToString(end) + ".bin";
      int fileHandle = FileOpen(fileName, FILE_WRITE | FILE_BIN | FILE_COMMON);
      if(fileHandle != INVALID_HANDLE)
      {
         // Prepare binary data in memory
         int headerSize = 8;
         int recordSize = 8 + 8 + 8 + 8 + 1;  // long + double + double + double + bool
         int totalSize = headerSize + copied * recordSize;
         uchar data[];
         ArrayResize(data, totalSize);
         int pos = 0;

         // Chronos Magic Header 0x53524843 ("CHRS") + Version 1
         IntToArrayLittleEndian(1397901379, data, pos); pos += 4;
         IntToArrayLittleEndian(1, data, pos); pos += 4;

         for(int i=0; i<copied; i++)
         {
            long netTicks = (ticks[i].time_msc * 10000) + 621355968000000000;
            LongToArrayLittleEndian(netTicks, data, pos); pos += 8;
            DoubleToArrayLittleEndian(ticks[i].bid, data, pos); pos += 8;
            DoubleToArrayLittleEndian(ticks[i].ask, data, pos); pos += 8;
            double vol = (ticks[i].volume_real > 0) ? ticks[i].volume_real : (double)ticks[i].volume;
            DoubleToArrayLittleEndian(vol, data, pos); pos += 8;
            data[pos++] = 0;   // isSynthetic = false
         }
         FileWriteArray(fileHandle, data, 0, totalSize);
         FileClose(fileHandle);

         string absPath = TerminalInfoString(TERMINAL_COMMONDATA_PATH) + "/Files/" + fileName;
         string resp = StringFormat("{\"success\":true,\"filePath\":\"%s\",\"totalRecords\":%d}", absPath, copied);
         SendResponse(requestId, "fetchHistory", resp);
      }
      else
      {
         SendResponse(requestId, "fetchHistory", "{\"success\":false,\"error\":\"Failed to create binary file on disk\"}");
      }
   }
   else if(command == "executeOrder")
   {
      string symbol   = JsonGetSimple(payload, "symbol");
      string typeStr  = JsonGetSimple(payload, "type");
      double volume   = StringToDouble(JsonGetSimple(payload, "volume"));
      double price    = StringToDouble(JsonGetSimple(payload, "price"));
      double sl       = StringToDouble(JsonGetSimple(payload, "stopLoss"));
      double tp       = StringToDouble(JsonGetSimple(payload, "takeProfit"));

      ENUM_ORDER_TYPE orderType;
      if(typeStr == "Buy")            orderType = ORDER_TYPE_BUY;
      else if(typeStr == "Sell")      orderType = ORDER_TYPE_SELL;
      else if(typeStr == "BuyLimit")  orderType = ORDER_TYPE_BUY_LIMIT;
      else if(typeStr == "SellLimit") orderType = ORDER_TYPE_SELL_LIMIT;
      else if(typeStr == "BuyStop")   orderType = ORDER_TYPE_BUY_STOP;
      else if(typeStr == "SellStop")  orderType = ORDER_TYPE_SELL_STOP;
      else { SendResponse(requestId, "executeOrder", "{\"success\":false,\"error\":\"Invalid type\"}"); return; }

      if(price == 0)
      {
         if(orderType == ORDER_TYPE_BUY) price = SymbolInfoDouble(symbol, SYMBOL_ASK);
         else                            price = SymbolInfoDouble(symbol, SYMBOL_BID);
      }
      Trade.SetExpertMagicNumber(0);
      if(Trade.PositionOpen(symbol, orderType, volume, price, sl, tp, "Chronos"))
      {
         ulong ticket = Trade.ResultOrder();
         SendResponse(requestId, "executeOrder",
            StringFormat("{\"success\":true,\"ticket\":%I64u,\"executedPrice\":%f,\"executedVolume\":%f}",
                         ticket, price, volume));
      }
      else
      {
         SendResponse(requestId, "executeOrder",
            StringFormat("{\"success\":false,\"error\":\"%s\"}", Trade.ResultRetcodeDescription()));
      }
   }
   else if(command == "modifyOrder")
   {
      ulong  ticket = (ulong)StringToInteger(JsonGetSimple(payload, "ticket"));
      double sl     = StringToDouble(JsonGetSimple(payload, "sl"));
      double tp     = StringToDouble(JsonGetSimple(payload, "tp"));
      double price  = StringToDouble(JsonGetSimple(payload, "price"));

      if(!OrderSelect(ticket))
      {
         SendResponse(requestId, "modifyOrder", "{\"success\":false,\"error\":\"Order not found\"}");
         return;
      }
      if(Trade.OrderModify(ticket, price, sl, tp, ORDER_TIME_GTC, 0))
         SendResponse(requestId, "modifyOrder", "{\"success\":true}");
      else
         SendResponse(requestId, "modifyOrder",
            StringFormat("{\"success\":false,\"error\":\"%s\"}", Trade.ResultRetcodeDescription()));
   }
   else if(command == "closePosition")
   {
      ulong  ticket = (ulong)StringToInteger(JsonGetSimple(payload, "ticket"));
      double volume = StringToDouble(JsonGetSimple(payload, "volume"));

      if(!PositionSelectByTicket(ticket))
      {
         SendResponse(requestId, "closePosition", "{\"success\":false,\"error\":\"Position not found\"}");
         return;
      }
      if(Trade.PositionClose(ticket, (volume > 0) ? volume : PositionInfo.Volume()))
         SendResponse(requestId, "closePosition", "{\"success\":true}");
      else
         SendResponse(requestId, "closePosition",
            StringFormat("{\"success\":false,\"error\":\"%s\"}", Trade.ResultRetcodeDescription()));
   }
   else if(command == "cancelOrder")
   {
      ulong ticket = (ulong)StringToInteger(JsonGetSimple(payload, "ticket"));
      if(!OrderSelect(ticket))
      {
         SendResponse(requestId, "cancelOrder", "{\"success\":false,\"error\":\"Order not found\"}");
         return;
      }
      if(Trade.OrderDelete(ticket))
         SendResponse(requestId, "cancelOrder", "{\"success\":true}");
      else
         SendResponse(requestId, "cancelOrder",
            StringFormat("{\"success\":false,\"error\":\"%s\"}", Trade.ResultRetcodeDescription()));
   }
   else if(command == "accountInfo")
   {
      double balance = AccountInfoDouble(ACCOUNT_BALANCE);
      double equity  = AccountInfoDouble(ACCOUNT_EQUITY);
      SendResponse(requestId, "accountInfo",
         StringFormat("{\"balance\":%f,\"equity\":%f}", balance, equity));
   }
   else if(command == "getPositions")
   {
      string posArray;
      StringInit(posArray, 1024);
      int total = PositionsTotal();
      for(int i=0; i<total; i++)
      {
         ulong ticket = PositionGetTicket(i);
         if(PositionSelectByTicket(ticket))
         {
            if(i > 0) StringAdd(posArray, ",");
            StringAdd(posArray, PositionToJson());
         }
      }
      SendResponse(requestId, "getPositions", "{\"positions\":[" + posArray + "]}");
   }
   else if(command == "getPendingOrders")
   {
      string ordArray;
      StringInit(ordArray, 1024);
      int total = OrdersTotal();
      for(int i=0; i<total; i++)
      {
         ulong ticket = OrderGetTicket(i);
         if(OrderSelect(ticket))
         {
            if(i > 0) StringAdd(ordArray, ",");
            StringAdd(ordArray, OrderToJson());
         }
      }
      SendResponse(requestId, "getPendingOrders", "{\"orders\":[" + ordArray + "]}");
   }
   else if(command == "symbolProperties")
   {
      string symbol = JsonGetSimple(payload, "symbol");
      string props = GetSymbolPropertiesJson(symbol);
      SendResponse(requestId, "symbolProperties", "{\"props\":" + props + "}");
   }
}

//+------------------------------------------------------------------+
//| One‑pass extraction of top‑level "requestId", "command", "payload"|
//+------------------------------------------------------------------+
bool JsonGetTopFields(string json, string &requestId, string &command, string &payload)
{
   requestId = "";
   command = "";
   payload = "";
   int len = StringLen(json);
   int pos = 0;
   while(pos < len)
   {
      // skip whitespace
      while(pos < len && (GetChar(json,pos)==' ' || GetChar(json,pos)=='\t' || GetChar(json,pos)=='\r' || GetChar(json,pos)=='\n')) pos++;
      if(pos >= len || GetChar(json,pos)!='"') break;
      int keyStart = ++pos;
      while(pos < len && GetChar(json,pos)!='"') pos++;
      if(pos >= len) break;
      string key = StringSubstr(json, keyStart, pos - keyStart);
      pos++; // skip closing quote
      // skip ':' and whitespace
      while(pos < len && GetChar(json,pos)!=':') pos++;
      pos++; // skip ':'
      while(pos < len && (GetChar(json,pos)==' ' || GetChar(json,pos)=='\t')) pos++;

      if(GetChar(json,pos)=='"')
      {
         int valStart = ++pos;
         while(pos < len && GetChar(json,pos)!='"') pos++;
         string val = StringSubstr(json, valStart, pos - valStart);
         pos++; // skip closing quote
         if(key == "requestId") requestId = val;
         else if(key == "command") command = val;
         else if(key == "payload") payload = val;
      }
      else if(GetChar(json,pos)=='{' || GetChar(json,pos)=='[')
      {
         int brace = 1;
         int start = pos;
         pos++;
         while(pos < len && brace > 0)
         {
            char c = GetChar(json,pos);
            if(c=='{' || c=='[') brace++;
            else if(c=='}' || c==']') brace--;
            pos++;
         }
         string val = StringSubstr(json, start, pos - start);
         if(key == "payload") payload = val;
      }
      else
      {
         int start = pos;
         while(pos < len && GetChar(json,pos)!=',' && GetChar(json,pos)!='}') pos++;
         string val = StringSubstr(json, start, pos - start);
         if(key == "command") command = val;
      }

      while(pos < len && GetChar(json,pos)!=',' && GetChar(json,pos)!='}') pos++;
      if(pos < len && GetChar(json,pos)==',') pos++;
   }
   return (StringLen(command) > 0);
}

//+------------------------------------------------------------------+
//| Extract a simple string value from a JSON object substring        |
//+------------------------------------------------------------------+
string JsonGetSimple(string jsonObj, string key)
{
   string search = "\"" + key + "\"";
   int pos = StringFind(jsonObj, search);
   if(pos < 0) return "";
   pos = StringFind(jsonObj, ":", pos + StringLen(search));
   if(pos < 0) return "";
   pos++;
   while(pos < StringLen(jsonObj))
   {
      ushort ch = StringGetCharacter(jsonObj, pos);
      if(ch==' ' || ch=='\t') pos++;
      else break;
   }
   if(StringGetCharacter(jsonObj, pos)=='"')
   {
      int start = pos+1;
      int end = StringFind(jsonObj, "\"", start);
      if(end < 0) return "";
      return StringSubstr(jsonObj, start, end - start);
   }
   return "";
}

//+------------------------------------------------------------------+
//| Send response – single combined send                              |
//+------------------------------------------------------------------+
void SendResponse(string requestId, string command, string payloadJson)
{
   string full = StringFormat("{\"requestId\":\"%s\",\"command\":\"%s\",\"payload\":%s}",
                              requestId, command, payloadJson);
   uchar body[];
   StringToCharArray(full, body, 0, StringLen(full));
   int len = ArraySize(body);

   uchar packet[];
   ArrayResize(packet, 4 + len);
   IntToArrayLittleEndian(len, packet, 0);
   ArrayCopy(packet, body, 4, 0, len);

   send(ClientSocket, packet, 4 + len, 0);
}

//+------------------------------------------------------------------+
//| Little‑endian byte helpers (inline)                               |
//+------------------------------------------------------------------+
void IntToArrayLittleEndian(int value, uchar &arr[], int start)
{
   arr[start]   = (uchar)(value & 0xFF);
   arr[start+1] = (uchar)((value >> 8) & 0xFF);
   arr[start+2] = (uchar)((value >> 16) & 0xFF);
   arr[start+3] = (uchar)((value >> 24) & 0xFF);
}

void LongToArrayLittleEndian(long value, uchar &arr[], int start)
{
   arr[start]   = (uchar)(value & 0xFF);
   arr[start+1] = (uchar)((value >> 8) & 0xFF);
   arr[start+2] = (uchar)((value >> 16) & 0xFF);
   arr[start+3] = (uchar)((value >> 24) & 0xFF);
   arr[start+4] = (uchar)((value >> 32) & 0xFF);
   arr[start+5] = (uchar)((value >> 40) & 0xFF);
   arr[start+6] = (uchar)((value >> 48) & 0xFF);
   arr[start+7] = (uchar)((value >> 56) & 0xFF);
}

void DoubleToArrayLittleEndian(double value, uchar &arr[], int start)
{
   DoubleBytes u;
   u.d = value;
   for(int i=0; i<8; i++)
      arr[start+i] = u.c[i];
}

//+------------------------------------------------------------------+
//| Serialization helpers                                             |
//+------------------------------------------------------------------+
string MqlTypeToChronosType(int mqlType)
{
   switch(mqlType)
   {
      case POSITION_TYPE_BUY:       return "Buy";
      case POSITION_TYPE_SELL:      return "Sell";
      case ORDER_TYPE_BUY_LIMIT:    return "BuyLimit";
      case ORDER_TYPE_SELL_LIMIT:   return "SellLimit";
      case ORDER_TYPE_BUY_STOP:     return "BuyStop";
      case ORDER_TYPE_SELL_STOP:    return "SellStop";
      default:                      return "Buy";
   }
}

string PositionToJson()
{
   return StringFormat(
      "{\"ticket\":%I64u,\"symbol\":\"%s\",\"type\":\"%s\",\"volume\":%f,\"openPrice\":%f,\"openTime\":%I64d,\"closePrice\":%f,\"closeTime\":%I64d,\"sl\":%f,\"tp\":%f,\"commission\":%f,\"swap\":%f,\"profit\":%f}",
      PositionGetInteger(POSITION_TICKET),
      PositionGetString(POSITION_SYMBOL),
      MqlTypeToChronosType((int)PositionGetInteger(POSITION_TYPE)),
      PositionGetDouble(POSITION_VOLUME),
      PositionGetDouble(POSITION_PRICE_OPEN),
      PositionGetInteger(POSITION_TIME),
      PositionGetDouble(POSITION_PRICE_CURRENT),
      0,
      PositionGetDouble(POSITION_SL),
      PositionGetDouble(POSITION_TP),
      PositionGetDouble(POSITION_COMMISSION),
      PositionGetDouble(POSITION_SWAP),
      PositionGetDouble(POSITION_PROFIT)
   );
}

string OrderToJson()
{
   return StringFormat(
      "{\"ticket\":%I64u,\"symbol\":\"%s\",\"type\":\"%s\",\"volume\":%f,\"price\":%f,\"sl\":%f,\"tp\":%f}",
      OrderGetInteger(ORDER_TICKET),
      OrderGetString(ORDER_SYMBOL),
      MqlTypeToChronosType((int)OrderGetInteger(ORDER_TYPE)),
      OrderGetDouble(ORDER_VOLUME_CURRENT),
      OrderGetDouble(ORDER_PRICE_OPEN),
      OrderGetDouble(ORDER_SL),
      OrderGetDouble(ORDER_TP)
   );
}

string GetSymbolPropertiesJson(string symbol)
{
   double tickSize     = SymbolInfoDouble(symbol, SYMBOL_TRADE_TICK_SIZE);
   double tickValue    = SymbolInfoDouble(symbol, SYMBOL_TRADE_TICK_VALUE);
   double contractSize = SymbolInfoDouble(symbol, SYMBOL_TRADE_CONTRACT_SIZE);
   double minVolume    = SymbolInfoDouble(symbol, SYMBOL_VOLUME_MIN);
   double maxLeverage  = (double)AccountInfoInteger(ACCOUNT_LEVERAGE);
   double swapLong     = SymbolInfoDouble(symbol, SYMBOL_SWAP_LONG);
   double swapShort    = SymbolInfoDouble(symbol, SYMBOL_SWAP_SHORT);

   return StringFormat(
      "{\"assetClass\":\"Forex\",\"marginMode\":\"Cross\",\"pendingTrigger\":\"UseAskForBuy\",\"marginCurrency\":\"%s\",\"contractSize\":%f,\"tickSize\":%f,\"tickValue\":%f,\"minVolume\":%f,\"maxLeverage\":%f,\"swapLong\":%f,\"swapShort\":%f}",
      AccountInfoString(ACCOUNT_CURRENCY),
      contractSize, tickSize, tickValue, minVolume, maxLeverage, swapLong, swapShort
   );
}

// Helper to safely get a character by index (kept for readability)
char GetChar(string text, int index)
{
   return (char)StringGetCharacter(text, index);
}
//+------------------------------------------------------------------+
