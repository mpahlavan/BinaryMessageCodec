using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace BinaryMessageCodec.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class CodecBenchmarks
{
    private readonly IMessageCodec _codec = new MessageCodec();
    private Message _smallMessage = null!;
    private Message _mediumMessage = null!;
    private Message _largeMessage = null!;
    private byte[] _smallEncoded = null!;
    private byte[] _mediumEncoded = null!;
    private byte[] _largeEncoded = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Small message: 3 headers, 100 byte payload
        _smallMessage = new Message
        {
            Headers = new Dictionary<string, string>
            {
                { "Session-ID", "abc123" },
                { "Content-Type", "application/sdp" },
                { "User-Agent", "TestClient/1.0" }
            },
            Payload = new byte[100]
        };
        _smallEncoded = _codec.Encode(_smallMessage);

        // Medium message: 20 headers, 10 KB payload
        _mediumMessage = new Message
        {
            Headers = new Dictionary<string, string>(),
            Payload = new byte[10 * 1024]
        };
        for (int i = 0; i < 20; i++)
        {
            _mediumMessage.Headers[$"Header-{i}"] = $"Value-{i}-" + new string('x', 50);
        }
        _mediumEncoded = _codec.Encode(_mediumMessage);

        // Large message: 50 headers, 100 KB payload
        _largeMessage = new Message
        {
            Headers = new Dictionary<string, string>(),
            Payload = new byte[100 * 1024]
        };
        for (int i = 0; i < 50; i++)
        {
            _largeMessage.Headers[$"Header-{i}"] = $"Value-{i}-" + new string('x', 100);
        }
        _largeEncoded = _codec.Encode(_largeMessage);
    }

    [Benchmark]
    public byte[] Encode_SmallMessage() => _codec.Encode(_smallMessage);

    [Benchmark]
    public byte[] Encode_MediumMessage() => _codec.Encode(_mediumMessage);

    [Benchmark]
    public byte[] Encode_LargeMessage() => _codec.Encode(_largeMessage);

    [Benchmark]
    public Message Decode_SmallMessage() => _codec.Decode(_smallEncoded);

    [Benchmark]
    public Message Decode_MediumMessage() => _codec.Decode(_mediumEncoded);

    [Benchmark]
    public Message Decode_LargeMessage() => _codec.Decode(_largeEncoded);

    [Benchmark]
    public Message RoundTrip_SmallMessage()
    {
        var encoded = _codec.Encode(_smallMessage);
        return _codec.Decode(encoded);
    }
}

// Uncomment to use BenchmarkDotNet (requires SDK path configuration)
// public class Program
// {
//     public static void Main(string[] args)
//     {
//         BenchmarkRunner.Run<CodecBenchmarks>();
//     }
// }
