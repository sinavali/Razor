//+------------------------------------------------------------------+
//|                                                  winsock.mqh     |
//|                      Copyright 2026, Chronos Co.                 |
//+------------------------------------------------------------------+
#property copyright "Copyright 2026, Chronos Co."
#property link      "https://chronos.com"

// Structures must be defined BEFORE they are used in #import
struct WSADATA
{
   ushort wVersion;
   ushort wHighVersion;
   char   szDescription[257];
   char   szSystemStatus[129];
   ushort iMaxSockets;
   ushort iMaxUdpDg;
   char   lpVendorInfo;
};

struct sockaddr_in
{
   short  sin_family;
   ushort sin_port;
   int    sin_addr;
   char   sin_zero[8];
};

#import "ws2_32.dll"
   int  WSAStartup(int wVersionRequested, WSADATA& lpWSAData);
   int  WSACleanup(void);
   int  WSAGetLastError(void);
   int  socket(int af, int type, int protocol);
   int  bind(int s, sockaddr_in& name, int namelen);
   int  listen(int s, int backlog);
   int  accept(int s, sockaddr_in& addr, int& addrlen);
   int  closesocket(int s);
   int  send(int s, uchar& buf[], int len, int flags);
   int  recv(int s, uchar& buf[], int len, int flags);
   int  ioctlsocket(int s, int cmd, uint& argp);
   int  htons(int hostshort);
   int  inet_addr(string cp);
#import

#define AF_INET         2
#define SOCK_STREAM     1
#define INVALID_SOCKET  -1
#define SOCKET_ERROR    -1
#define INADDR_ANY      0
#define FIONBIO         0x5421
#define WSAEWOULDBLOCK  10035