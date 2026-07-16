# Razor – Project Overview

**Version:** 1.0.0 LTS  
**Audience:** Investors, partners, and prospective users  
**Status:** Authoritative  
**Last Updated:** 2026-07-09  

---

## 1. What Is Razor?

Razor is an **institutional‑grade strategy optimisation and automated trading engine**. At its heart, it uses advanced computational intelligence – genetic algorithms, neural networks, and rigorous statistical analysis – to automatically evolve and refine trading strategies. It is not a broker or a charting platform; it is the analytical brain that sits on top of a trader's existing execution infrastructure, making it smarter, more systematic, and continuously improving.

Traders and quants deploy a lightweight Razor Engine on their own server, where it connects to any brokerage or trading platform they already use. Everything else – strategy configuration, optimisation scheduling, live monitoring, and report generation – is managed through a cloud‑based control panel, accessible from anywhere.

---

## 2. The Gap Razor Fills

Professional algorithmic trading has long been split between two worlds:

- **Retail‑grade tools** (MetaTrader, TradingView, NinjaTrader) are easy to start with but quickly become restrictive. Their backtesting is often bar‑based, their optimisation is rudimentary, and their reporting rarely goes beyond a basic trade list. Serious traders outgrow them within months.
- **Institutional systems** (custom in‑house frameworks, Deltix, AlgoTrader) offer the rigour professionals need, but they come with staggering costs and require dedicated engineering teams to build and maintain. For independent quants, small funds, and professional trading teams without a seven‑figure technology budget, these systems are out of reach.

Between these extremes, there is no widely available solution that delivers **institutional‑grade optimisation, live monitoring, and rich reporting** in a single, remotely managed package. Razor exists to fill that gap.

---

## 3. How Razor Works – A Bird's‑Eye View

A trader installs the Razor Engine on their own server – a Windows or Linux machine they control. The engine connects to the trader's existing broker or trading platform (MetaTrader, cTrader, Binance, Interactive Brokers, etc.) through small extension modules called adapters.

The trader then opens the **Razor Cloud** dashboard in a web browser. From this single interface, they can:

- Configure a trading strategy and launch an optimisation run.
- Watch the genetic algorithm evolve thousands of candidate solutions in real time.
- Review detailed performance reports, including equity curves, drawdown analysis, and risk‑adjusted metrics.
- Deploy the optimised strategy for live trading and monitor its every move – positions, orders, equity, margin – without ever touching the server.

All heavy computation – backtesting millions of tick records, evaluating populations of strategies, training neural networks – happens on the trader's hardware. The Cloud provides the interface, the scheduling, the data storage, and the remote‑control capability. This architecture keeps trading logic and sensitive credentials within the trader's own environment while providing the convenience of a fully managed cloud service.

Developers build strategies, adapters, indicators, hook plugins, and neural network models using a clean public SDK (`Razor.Sdk`). The same engine binary serves both development and production; the only difference is the license permission set, making the transition from testing to live seamless.

---

## 4. The Technology Inside Razor

Razor is built on a modern, deterministic software foundation designed for scientific accuracy and reproducibility. Every backtest, every optimisation, produces identical results given the same starting conditions – a non‑negotiable requirement for strategy validation.

Its core technologies include:

- **Genetic Algorithms (GA)** – A population‑based optimisation method inspired by biological evolution. Thousands of strategy variations are generated, evaluated against historical data, and the best performers are "bred" together over successive generations. Mutations and cross‑over operations introduce diversity, while elitism preserves the strongest solutions. Razor's GA includes advanced features like stagnation detection, hyper‑mutation, and walk‑forward analysis, ensuring robust, non‑curve‑fitted results.

- **Artificial Neural Networks (ANN)** – Deep learning models that can be embedded directly into trading strategies. These networks learn patterns from tick data and market conditions, providing signals that traditional rule‑based logic cannot easily capture. Razor supports a unified parameter‑vector interface (`INeuralNetworkModel`) compatible with feed‑forward, ONNX, LSTM, and RL architectures. The GA can optimise the network's weights alongside the strategy's parameters, co‑evolving the entire system.

- **Hooks System** – A priority‑based extensibility mechanism inspired by WordPress. Hook plugins can register filter callbacks (to transform or reject data) and action callbacks (to observe events) at over 50 named hook points throughout the backtest, live, optimisation, and report pipelines. This enables custom risk management, notifications, metrics, reporting, trailing stops, and more—all without modifying the engine.

- **Tick‑Only Core** – All operations use raw tick data, not bars. OHLC statistics are aggregated on‑demand from the tick stream, ensuring maximum fidelity and flexibility. This design allows strategies to react to every price movement and supports high‑frequency and low‑latency trading strategies.

- **Advanced Statistical Analytics** – A comprehensive metrics library computes Sharpe, Sortino, and Calmar ratios, profit factor, win rate, and maximum drawdown, both at the portfolio level and per‑symbol. Correlation matrices show how strategies interact, while Monte Carlo analysis tests robustness under random trade sequences.

These technologies are not separate products requiring integration; they are a single, coherent engine that the trader accesses through a unified interface.

---

## 5. The Competitive Landscape

Razor does not have a direct, like‑for‑like competitor. The platforms that come closest are well‑known to every professional trader, but they serve different primary purposes:

- **MetaTrader 4/5** – The most widely used retail trading platform globally. Excellent for manual and simple automated trading, but its backtesting is bar‑based, its optimisation is limited to a brute‑force single‑pass method, and its reporting consists of a static HTML statement with no interactive depth. It also lacks remote management and advanced ML integration.

- **TradingView** – A powerful charting and social platform with a popular scripting language (Pine Script). Its strategy tester provides lightweight backtesting but lacks a serious optimisation engine, genetic algorithms, or walk‑forward analysis. It cannot serve as a remote‑controlled live trading command centre.

- **QuantConnect** – A cloud‑based algorithmic trading platform built on the open‑source LEAN engine. It offers backtesting and some optimisation capabilities, but the engine runs on QuantConnect's servers. Traders cannot deploy a private engine on their own hardware with cloud‑based control. This raises data privacy concerns for institutional users.

- **NinjaTrader, Amibroker, cTrader** – Strong in specific niches (futures, equities, CFD trading respectively). Each has scripting capabilities and basic backtesting, but none makes advanced optimisation or continuous live re‑optimisation a core mission. Reporting is functional but not a strategic differentiator.

- **Custom In‑House Systems** – Many hedge funds and proprietary trading firms build their own backtesting and execution frameworks. This approach offers maximum flexibility but requires significant engineering resources and ongoing maintenance, making it impractical for smaller teams.

None of these platforms treat **strategy optimisation as their primary reason for existing**. None offer a combination of private engine deployment, cloud‑based control, an extension marketplace, and institutional‑depth reporting. Razor uniquely occupies this intersection.

---

## 6. Why Deep Reporting and Monitoring Matter

For a professional trader, reporting is not an afterthought – it is the product. Decisions about capital allocation, risk limits, and strategy viability depend entirely on the quality and depth of the analytics available.

Razor treats reporting as a first‑class feature. Every trade, every tick, every generation of an optimisation run produces data that is stored, processed, and made available for interactive exploration. Traders can drill into per‑symbol performance, examine correlation heatmaps, reconstruct equity curves from any starting point, and export fully formatted reports (Excel, JSON, PDF) with a single click – all without needing a spreadsheet or a statistics package.

Real‑time monitoring extends this philosophy to live trading. The Cloud dashboard shows exactly what the engine is doing, second by second: open positions, pending orders, current drawdown, margin utilisation, and health status. Alerts can be configured to fire on predefined conditions – a margin call, an unexpected disconnect, a drawdown threshold breach – ensuring the trader is always informed, even when away from the screen.

---

## 7. The Razor Ecosystem

Razor is not just a standalone engine; it is an ecosystem designed for extensibility and community growth:

- **Public SDK (`Razor.Sdk`)** – A NuGet package containing only contracts (interfaces, base classes, enums, and helpers). Developers can build extensions without touching engine internals.

- **Extension Marketplace** – A separate service for discovering, licensing, and distributing extensions (adapters, strategies, indicators, hook plugins, and neural network models). Developers can monetise their work, and users can easily find and install extensions.

- **Cloud‑First Architecture** – All configuration, scheduling, and reporting reside in the Cloud. The Engine is a thin, stateless execution node. This design simplifies deployment, updates, and management.

---

## 8. Target Audience

Razor is designed for three primary user groups:

1. **Independent Quants and Traders** – Individuals developing and deploying their own algorithmic strategies. They need a powerful yet accessible toolset that does not require a full‑time engineering team.

2. **Proprietary Trading Firms and Hedge Funds** – Teams with existing trading infrastructure who want to enhance their strategies with advanced optimisation and ML, while retaining control over their data and execution environment.

3. **Brokers and Exchanges** – Institutional partners who wish to offer advanced algorithm development and optimisation capabilities to their clients, increasing engagement and trading volume.

---

## 9. Business Model

Razor is sold as a per‑user subscription with tiered pricing based on features and usage limits:

- **Free Developer Tier** – Full access to the SDK and Engine with limitations (mock adapter for live trading, limited historical data range). Ideal for learning and development.

- **Professional Tier** – Full live trading capabilities, unlimited backtests, and standard support. Includes one engine instance.

- **Enterprise Tier** – Multiple engine instances, advanced scheduling, custom report templates, priority support, and dedicated account management.

Additional revenue streams include Marketplace transaction fees on extension sales and professional services for custom adapter and strategy development.

---

## 10. Vision

Razor aims to become the **default optimisation and monitoring layer** for professional algorithmic trading worldwide. Whether a trader uses MetaTrader, cTrader, a cryptocurrency exchange, or a prime brokerage API, Razor provides the intelligence layer that makes their trading systematic, measurable, and continuously improving – without requiring them to build or maintain any of it themselves.

---

*This document introduces Razor at a high level. For technical specifications, see the [Razor Engine Technical Blueprint](core/docs/Razor%20Engine%20–%20Finalised%20Technical%20Blueprint.md) and [Internal Architecture Document](core/docs/Razor%20Internal%20Technical%20Architecture%20Document.md). For product details, see the [Razor Product Model](Razor%20Product%20Model.md).*