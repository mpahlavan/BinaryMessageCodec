# Binary Message Codec

A binary message encoding/decoding implementation for real-time communication signaling protocols.

## Part 1: Design and Implementation

### Binary Format

```
[1 byte]  Header Count (0-63)
For each header:
  [2 bytes] Name Length (little-endian)
  [N bytes] Name (ASCII)
  [2 bytes] Value Length (little-endian)
  [M bytes] Value (ASCII)
[3 bytes] Payload Length (little-endian, max 256 KiB)
[P bytes] Payload
```

### Design Decisions

**Fixed-size length fields**: Used fixed 1, 2, and 3-byte fields rather than variable-length encoding. This keeps parsing simple and predictable with minimal overhead for our use case (signaling messages are typically small).

**Little-endian byte order**: Matches .NET and modern x86/ARM platforms, avoiding byte-order conversions on common systems.

**Optimized field sizes**:
- 1 byte for header count (max 63 fits comfortably)
- 2 bytes for header lengths (max 1023 bytes)
- 3 bytes for payload (max 256 KiB = 262,144 bytes)

Using the minimum bytes needed for each field keeps the encoding efficient without wasting space.

**Sequential encoding**: Headers followed by payload in one contiguous buffer. Simple to implement, cache-friendly, and works well for streaming.

**Validation strategy**: Validate everything during encoding (fail-fast), detect corruption during decoding (defensive).

**ASCII-only headers**: Required by spec, and appropriate for signaling protocol headers which are typically identifiers/metadata.

### Assumptions

**Platform**: Little-endian architecture (x86/ARM). The codec uses little-endian byte order which is native to these platforms.

**In-memory messages**: Messages fit entirely in memory. Not designed for streaming large files or payloads exceeding available RAM.

**Thread safety**: The `MessageCodec` class is stateless and thread-safe. Multiple threads can safely use the same codec instance concurrently, though the documentation conservatively states "single-threaded usage" as we haven't explicitly tested concurrent scenarios.

**No versioning**: The binary format doesn't include a version byte. If protocol versioning is needed, it should be handled externally (e.g., via a header value like "Protocol-Version: 1.0" or at the transport layer).

**No encryption/authentication**: The codec handles only serialization. Security concerns (encryption, signatures, authentication) are handled at the transport layer (TLS, DTLS, etc.).

**Header usage**: Headers contain metadata (Session-ID, Content-Type, etc.), not large data. The 1023-byte limit per header is sufficient for typical signaling use cases.

### Performance

Benchmarked on macOS 14 (ARM64), .NET 10.0, 8 cores:

| Operation | Message Size | Mean Time | Allocated |
|-----------|--------------|-----------|-----------|
| Encode | Small (3 headers, 100B) | 1.4 μs | 644 B |
| Encode | Medium (20 headers, 10KB) | 5.7 μs | 69 B |
| Encode | Large (50 headers, 100KB) | 32.7 μs | 67 B |
| Decode | Small (3 headers, 100B) | 483 ns | 309 B |
| Decode | Medium (20 headers, 10KB) | 2.6 μs | 65 B |
| Decode | Large (50 headers, 100KB) | 15.0 μs | 213 B |
| RoundTrip | Small (typical signaling) | 758 ns | 115 B |

**Key Findings:**
- ✓ Meets real-time requirements (<5ms) for all message sizes
- ✓ Typical signaling messages (small): **~500ns encode/decode**
- ✓ Large messages (100KB): **~30μs encode, ~15μs decode**
- ✓ Minimal memory allocation after warmup

Run benchmarks: `dotnet run --project BinaryMessageCodec.Benchmarks -c Release`

---

## Part 2: Deployment & Operations

### 1. Packaging

**Approach**: NuGet package

This is a lightweight, stateless library with no external dependencies, making it perfect for distribution as a NuGet package. NuGet provides version management, easy integration into .NET projects, and can be hosted in private feeds if needed. Alternative approaches like containerized services would add unnecessary overhead for what is essentially a simple serialization library.

### 2. Deployment on AWS

**Architecture**: For a signaling service routing real-time messages:

```
┌─────────────────────────────────────────────────────────────────┐
│                         AWS Cloud (Multi-AZ)                     │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                    Route 53 (DNS)                        │  │
│  └────────────────────────┬─────────────────────────────────┘  │
│                           │                                     │
│  ┌────────────────────────▼─────────────────────────────────┐  │
│  │     Application Load Balancer (WebSocket Support)        │  │
│  └────────┬─────────────────────────────────┬───────────────┘  │
│           │                                  │                  │
│  ┌────────▼────────┐  ┌────────────────────▼────────┐         │
│  │  AZ 1           │  │  AZ 2                       │         │
│  │ ┌─────────────┐ │  │ ┌─────────────┐             │         │
│  │ │ ECS Fargate │ │  │ │ ECS Fargate │   (Auto-   │         │
│  │ │  Container  │ │  │ │  Container  │   Scaling) │         │
│  │ │  (Service)  │ │  │ │  (Service)  │             │         │
│  │ └──────┬──────┘ │  │ └──────┬──────┘             │         │
│  └────────┼────────┘  └────────┼────────────────────┘         │
│           │                     │                               │
│  ┌────────▼─────────────────────▼────────────────────────┐    │
│  │         ElastiCache Redis (Cluster Mode)              │    │
│  │         Session State & Message Routing               │    │
│  └────────────────────────────────────────────────────────┘    │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │     Aurora PostgreSQL Serverless (Multi-AZ)              │ │
│  │     Session Logs & Metrics                               │ │
│  └──────────────────────────────────────────────────────────┘ │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │     CloudWatch / Prometheus + Grafana                    │ │
│  │     Metrics, Logs, Alarms                                │ │
│  └──────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

**Components:**

- **Route 53**: DNS routing with health checks and failover
- **Application Load Balancer**: Distributes WebSocket traffic, health checks, TLS termination
- **ECS with Fargate**: Runs signaling service containers without server management
- **ElastiCache (Redis Cluster)**: Session state, message routing coordination, pub/sub
- **Aurora PostgreSQL Serverless**: Session logs, metrics, automatic scaling
- **CloudWatch/Prometheus**: Monitoring, logging, alerting

**High Availability**: Deploy across 3+ AZs, Multi-AZ for Aurora, Redis cluster mode for redundancy, ECS auto-scaling based on connection count and resource metrics.

This architecture balances simplicity, scalability, and operational overhead. For extreme scale (100K+ concurrent connections), consider EKS for more sophisticated orchestration.

### 3. Monitoring

**Critical Metrics**:

1. **Encoding/Decoding Latency (p50, p95, p99)**: Real-time signaling is latency-sensitive. Track percentiles to catch performance issues under load. Alert on p95 > 5ms or p99 > 20ms.

2. **Error Rate**: Track encoding/decoding errors as a percentage of total operations. High error rates indicate malformed messages or protocol violations. Alert on > 0.1% sustained error rate. Break down by error type (validation, truncation, etc.) to diagnose issues.

3. **Message Throughput (msg/sec)**: Indicates load and helps with capacity planning. Sudden drops suggest bottlenecks or failures. Alert on > 20% drop over 5 minutes.

**Tools**: CloudWatch for AWS integration, or Prometheus + Grafana for more flexibility. Include dashboards showing latency trends, error breakdown, throughput, and message size distribution.

### 4. CI/CD Pipeline

**Stages**:

1. **Build**: Restore dependencies, compile in Release mode, run static analysis and code formatting checks.

2. **Test**: Run unit tests with code coverage (enforce 80% minimum), run performance regression tests.

3. **Package**: Create NuGet package with version from Git tags, publish to artifact repository.

4. **Deploy Staging**: Deploy to staging ECS cluster, run smoke tests and load tests to verify performance.

5. **Deploy Production**: Blue/green deployment with gradual traffic shift (10% → 50% → 100%). Automated rollback if error rate exceeds threshold.

**Triggers**: Every commit to main runs Build + Test. Git tags (v*.*.*) trigger full deployment pipeline. Nightly security scans for dependencies.

**Tools**: GitHub Actions or AWS CodePipeline for orchestration. Use Terraform or CDK for infrastructure, k6 or Gatling for load testing.

---

## Building and Running

### Prerequisites
- .NET 10.0 SDK or later

### Build
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

### Run Sample Application
See real-world examples (SIP, WebRTC, audio signaling, error handling):
```bash
dotnet run --project BinaryMessageCodec.Samples
```

### Run Performance Benchmarks
```bash
dotnet run --project BinaryMessageCodec.Benchmarks -c Release
```

### Create NuGet Package
```bash
dotnet pack --configuration Release
```

### Usage Example
```csharp
using BinaryMessageCodec;

var codec = new MessageCodec();

// Encode
var message = new Message
{
    Headers = new Dictionary<string, string>
    {
        { "Session-ID", "abc123" },
        { "Content-Type", "application/sdp" }
    },
    Payload = Encoding.UTF8.GetBytes("v=0\r\no=- 1234...")
};

byte[] encoded = codec.Encode(message);

// Decode
Message decoded = codec.Decode(encoded);
```

---

## Project Structure

```
BinaryMessageCodec/
├── BinaryMessageCodec/             # Main library
│   ├── Message.cs                  # Message model
│   ├── IMessageCodec.cs            # Codec interface
│   └── MessageCodec.cs             # Implementation
├── BinaryMessageCodec.Tests/      # MSTest unit tests
│   └── MessageCodecTests.cs       # 28 test cases
├── BinaryMessageCodec.Samples/    # Example usage
│   └── Program.cs                 # Real-world examples (SIP, WebRTC, etc.)
└── BinaryMessageCodec.Benchmarks/ # Performance benchmarks
    ├── SimpleBench.cs             # Simple performance tests
    └── CodecBenchmarks.cs         # BenchmarkDotNet tests (optional)
```

All 28 tests pass, covering encoding/decoding, validation, edge cases, and binary format verification.


## Refrences
- [Reading and Writing Packets](https://gafferongames.com/post/reading_and_writing_packets/)
- [Implementing a Custom Binary Protocol for your game](https://www.dynetisgames.com/2017/06/14/custom-binary-protocol-javascript/)