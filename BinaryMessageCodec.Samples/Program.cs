using System.Text;
using BinaryMessageCodec;

Console.WriteLine("=== Binary Message Codec - Sample Usage ===\n");

var codec = new MessageCodec();

// Example 1: Simple SIP INVITE Message
Console.WriteLine("Example 1: SIP INVITE Message");
Console.WriteLine("------------------------------");
var sipInvite = new Message
{
    Headers = new Dictionary<string, string>
    {
        { "Call-ID", "f81d4fae-7dec-11d0-a765-00a0c91e6bf6" },
        { "CSeq", "1 INVITE" },
        { "From", "sip:alice@example.com" },
        { "To", "sip:bob@example.com" },
        { "Content-Type", "application/sdp" }
    },
    Payload = Encoding.UTF8.GetBytes("v=0\r\no=alice 2890844526 IN IP4 pc33.atlanta.com\r\ns=Session\r\n")
};

var encoded = codec.Encode(sipInvite);
Console.WriteLine($"Original: {sipInvite.Headers.Count} headers, {sipInvite.Payload.Length} bytes payload");
Console.WriteLine($"Encoded size: {encoded.Length} bytes (overhead: {encoded.Length - sipInvite.Payload.Length} bytes)");

var decoded = codec.Decode(encoded);
Console.WriteLine($"✓ Decoded successfully");
Console.WriteLine($"  Call-ID: {decoded.Headers["Call-ID"]}");
Console.WriteLine($"  From: {decoded.Headers["From"]}");
Console.WriteLine();

// Example 2: WebRTC Offer Message
Console.WriteLine("Example 2: WebRTC Offer Message");
Console.WriteLine("--------------------------------");
var webrtcOffer = new Message
{
    Headers = new Dictionary<string, string>
    {
        { "Session-ID", "webrtc-session-123" },
        { "Message-Type", "offer" },
        { "Timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() },
        { "User-Agent", "WebRTC-Client/1.0" }
    },
    Payload = Encoding.UTF8.GetBytes(@"{""type"":""offer"",""sdp"":""v=0\r\no=- 4611731400430051336 2 IN IP4 127.0.0.1""}")
};

encoded = codec.Encode(webrtcOffer);
decoded = codec.Decode(encoded);
Console.WriteLine($"✓ Encoded and decoded {decoded.Headers["Message-Type"]} message");
Console.WriteLine($"  Session: {decoded.Headers["Session-ID"]}");
Console.WriteLine($"  Payload: {Encoding.UTF8.GetString(decoded.Payload)[..60]}...");
Console.WriteLine();

// Example 3: Binary Payload (Simulated Audio Chunk)
Console.WriteLine("Example 3: Audio Signaling Message");
Console.WriteLine("-----------------------------------");
var audioChunk = new Message
{
    Headers = new Dictionary<string, string>
    {
        { "Codec", "opus" },
        { "Sample-Rate", "48000" },
        { "Channels", "2" },
        { "Sequence", "42" },
        { "Timestamp", "1234567890" }
    },
    Payload = new byte[1024] // Simulated audio metadata/chunk
};
new Random(42).NextBytes(audioChunk.Payload);

encoded = codec.Encode(audioChunk);
decoded = codec.Decode(encoded);
Console.WriteLine($"✓ Audio message: {decoded.Headers["Codec"]} @ {decoded.Headers["Sample-Rate"]}Hz");
Console.WriteLine($"  Channels: {decoded.Headers["Channels"]}, Sequence: {decoded.Headers["Sequence"]}");
Console.WriteLine($"  Payload size: {decoded.Payload.Length} bytes");
Console.WriteLine();

// Example 4: Edge Case - Maximum Headers
Console.WriteLine("Example 4: Maximum Headers (63)");
Console.WriteLine("--------------------------------");
var maxHeadersMsg = new Message
{
    Headers = new Dictionary<string, string>(),
    Payload = Encoding.UTF8.GetBytes("Small payload")
};

for (int i = 0; i < 63; i++)
{
    maxHeadersMsg.Headers[$"Header-{i}"] = $"Value-{i}";
}

encoded = codec.Encode(maxHeadersMsg);
decoded = codec.Decode(encoded);
Console.WriteLine($"✓ Successfully encoded/decoded {decoded.Headers.Count} headers");
Console.WriteLine($"  Total encoded size: {encoded.Length:N0} bytes");
Console.WriteLine();

// Example 5: Error Handling
Console.WriteLine("Example 5: Error Handling");
Console.WriteLine("-------------------------");

// Test 1: Invalid non-ASCII characters
try
{
    var invalidMsg = new Message
    {
        Headers = new Dictionary<string, string> { { "Invalid", "Hello 世界" } },
        Payload = Array.Empty<byte>()
    };
    codec.Encode(invalidMsg);
    Console.WriteLine("✗ Should have thrown exception!");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"✓ Caught expected error: {ex.Message}");
}

// Test 2: Too many headers
try
{
    var tooManyHeaders = new Message
    {
        Headers = new Dictionary<string, string>(),
        Payload = Array.Empty<byte>()
    };
    for (int i = 0; i < 64; i++) // 64 exceeds max of 63
    {
        tooManyHeaders.Headers[$"H{i}"] = "value";
    }
    codec.Encode(tooManyHeaders);
    Console.WriteLine("✗ Should have thrown exception!");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"✓ Caught expected error: {ex.Message}");
}

// Test 3: Corrupted data
try
{
    var corrupted = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
    codec.Decode(corrupted);
    Console.WriteLine("✗ Should have thrown exception!");
}
catch (InvalidDataException ex)
{
    Console.WriteLine($"✓ Caught expected error: {ex.Message}");
}

Console.WriteLine("\n=== All examples completed successfully! ===");
Console.WriteLine("\nTo run benchmarks: cd BinaryMessageCodec.Benchmarks && dotnet run -c Release");
