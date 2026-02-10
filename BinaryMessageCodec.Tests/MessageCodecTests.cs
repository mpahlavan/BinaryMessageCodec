using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BinaryMessageCodec.Tests;

[TestClass]
public class MessageCodecTests
{
    private readonly IMessageCodec _codec = new MessageCodec();

    #region Basic Encode/Decode Tests

    [TestMethod]
    public void EncodeAndDecode_EmptyMessage_ReturnsEquivalentMessage()
    {
        // Arrange
        var message = new Message
        {
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>()
        };

        // Act
        var encoded = _codec.Encode(message);
        var decoded = _codec.Decode(encoded);

        // Assert
        Assert.IsNotNull(decoded);
        Assert.AreEqual(0, decoded.Headers.Count);
        Assert.AreEqual(0, decoded.Payload.Length);
    }

    [TestMethod]
    public void EncodeAndDecode_MessageWithHeaders_ReturnsEquivalentMessage()
    {
        // Arrange
        var message = new Message
        {
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "User-Agent", "TestClient/1.0" },
                { "Accept", "text/html" }
            },
            Payload = Array.Empty<byte>()
        };

        // Act
        var encoded = _codec.Encode(message);
        var decoded = _codec.Decode(encoded);

        // Assert
        Assert.IsNotNull(decoded);
        Assert.AreEqual(3, decoded.Headers.Count);
        Assert.AreEqual("application/json", decoded.Headers["Content-Type"]);
        Assert.AreEqual("TestClient/1.0", decoded.Headers["User-Agent"]);
        Assert.AreEqual("text/html", decoded.Headers["Accept"]);
        Assert.AreEqual(0, decoded.Payload.Length);
    }

    [TestMethod]
    public void EncodeAndDecode_MessageWithPayload_ReturnsEquivalentMessage()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes("Hello, World! This is a test payload.");
        var message = new Message
        {
            Headers = new Dictionary<string, string>(),
            Payload = payload
        };

        // Act
        var encoded = _codec.Encode(message);
        var decoded = _codec.Decode(encoded);

        // Assert
        Assert.IsNotNull(decoded);
        Assert.AreEqual(0, decoded.Headers.Count);
        CollectionAssert.AreEqual(payload, decoded.Payload);
    }

    [TestMethod]
    public void EncodeAndDecode_CompleteMessage_ReturnsEquivalentMessage()
    {
        // Arrange
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var message = new Message
        {
            Headers = new Dictionary<string, string>
            {
                { "Session-ID", "abc123" },
                { "Protocol", "WebRTC" }
            },
            Payload = payload
        };

        // Act
        var encoded = _codec.Encode(message);
        var decoded = _codec.Decode(encoded);

        // Assert
        Assert.IsNotNull(decoded);
        Assert.AreEqual(2, decoded.Headers.Count);
        Assert.AreEqual("abc123", decoded.Headers["Session-ID"]);
        Assert.AreEqual("WebRTC", decoded.Headers["Protocol"]);
        CollectionAssert.AreEqual(payload, decoded.Payload);
    }

    #endregion

    #region Edge Case Tests

    [TestMethod]
    public void Encode_MessageWithMaxHeaders_Succeeds()
    {
        // Arrange
        var message = new Message
        {
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>()
        };

        // Add maximum number of headers (63)
        for (int i = 0; i < 63; i++)
        {
            message.Headers[$"Header{i}"] = $"Value{i}";
        }

        // Act
        var encoded = _codec.Encode(message);
        var decoded = _codec.Decode(encoded);

        // Assert
        Assert.AreEqual(63, decoded.Headers.Count);
    }

    [TestMethod]
    public void Encode_MessageWithMaxHeaderLength_Succeeds()
    {
        // Arrange
        var longName = new string('A', 1023);
        var longValue = new string('B', 1023);
        var message = new Message
        {
            Headers = new Dictionary<string, string>
            {
                { longName, longValue }
            },
            Payload = Array.Empty<byte>()
        };

        // Act
        var encoded = _codec.Encode(message);
        var decoded = _codec.Decode(encoded);

        // Assert
        Assert.AreEqual(1, decoded.Headers.Count);
        Assert.AreEqual(longValue, decoded.Headers[longName]);
    }

    [TestMethod]
    public void Encode_MessageWithMaxPayload_Succeeds()
    {
        // Arrange
        var maxPayload = new byte[256 * 1024]; // 256 KiB
        for (int i = 0; i < maxPayload.Length; i++)
        {
            maxPayload[i] = (byte)(i % 256);
        }

        var message = new Message
        {
            Headers = new Dictionary<string, string>(),
            Payload = maxPayload
        };

        // Act
        var encoded = _codec.Encode(message);
        var decoded = _codec.Decode(encoded);

        // Assert
        CollectionAssert.AreEqual(maxPayload, decoded.Payload);
    }

    [TestMethod]
    public void EncodeAndDecode_HeaderWithEmptyValue_ReturnsEquivalentMessage()
    {
        // Arrange
        var message = new Message
        {
            Headers = new Dictionary<string, string>
            {
                { "EmptyHeader", "" }
            },
            Payload = Array.Empty<byte>()
        };

        // Act
        var encoded = _codec.Encode(message);
        var decoded = _codec.Decode(encoded);

        // Assert
        Assert.AreEqual(1, decoded.Headers.Count);
        Assert.AreEqual("", decoded.Headers["EmptyHeader"]);
    }

    [TestMethod]
    public void EncodeAndDecode_HeaderWithSpecialASCIICharacters_ReturnsEquivalentMessage()
    {
        // Arrange
        var message = new Message
        {
            Headers = new Dictionary<string, string>
            {
                { "Special!@#", "Value$%^&*()" }
            },
            Payload = Array.Empty<byte>()
        };

        // Act
        var encoded = _codec.Encode(message);
        var decoded = _codec.Decode(encoded);

        // Assert
        Assert.AreEqual(1, decoded.Headers.Count);
        Assert.AreEqual("Value$%^&*()", decoded.Headers["Special!@#"]);
    }

    #endregion

    #region Validation Tests

    [TestMethod]
    public void Encode_NullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => _codec.Encode(null!));
    }

    [TestMethod]
    public void Encode_TooManyHeaders_ThrowsArgumentException()
    {
        // Arrange
        var message = new Message
        {
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>()
        };

        // Add 64 headers (exceeds limit of 63)
        for (int i = 0; i < 64; i++)
        {
            message.Headers[$"Header{i}"] = $"Value{i}";
        }

        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentException>(() => _codec.Encode(message));
        StringAssert.Contains(ex.Message, "63");
    }

    [TestMethod]
    public void Encode_HeaderNameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var longName = new string('A', 1024); // Exceeds 1023 limit
        var message = new Message
        {
            Headers = new Dictionary<string, string>
            {
                { longName, "Value" }
            },
            Payload = Array.Empty<byte>()
        };

        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentException>(() => _codec.Encode(message));
        StringAssert.Contains(ex.Message, "1023");
    }

    [TestMethod]
    public void Encode_HeaderValueTooLong_ThrowsArgumentException()
    {
        // Arrange
        var longValue = new string('B', 1024); // Exceeds 1023 limit
        var message = new Message
        {
            Headers = new Dictionary<string, string>
            {
                { "Name", longValue }
            },
            Payload = Array.Empty<byte>()
        };

        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentException>(() => _codec.Encode(message));
        StringAssert.Contains(ex.Message, "1023");
    }

    [TestMethod]
    public void Encode_HeaderNameWithNonASCII_ThrowsArgumentException()
    {
        // Arrange
        var message = new Message
        {
            Headers = new Dictionary<string, string>
            {
                { "Héader", "Value" } // Non-ASCII character
            },
            Payload = Array.Empty<byte>()
        };

        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentException>(() => _codec.Encode(message));
        StringAssert.Contains(ex.Message, "ASCII");
    }

    [TestMethod]
    public void Encode_HeaderValueWithNonASCII_ThrowsArgumentException()
    {
        // Arrange
        var message = new Message
        {
            Headers = new Dictionary<string, string>
            {
                { "Name", "Valüe" } // Non-ASCII character
            },
            Payload = Array.Empty<byte>()
        };

        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentException>(() => _codec.Encode(message));
        StringAssert.Contains(ex.Message, "ASCII");
    }

    [TestMethod]
    public void Encode_PayloadTooLarge_ThrowsArgumentException()
    {
        // Arrange
        var tooLargePayload = new byte[256 * 1024 + 1]; // Exceeds 256 KiB
        var message = new Message
        {
            Headers = new Dictionary<string, string>(),
            Payload = tooLargePayload
        };

        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentException>(() => _codec.Encode(message));
        StringAssert.Contains(ex.Message, "262144");
    }

    [TestMethod]
    public void Encode_EmptyHeaderName_ThrowsArgumentException()
    {
        // Arrange - Test with empty string since Dictionary doesn't allow null keys
        var message = new Message
        {
            Headers = new Dictionary<string, string>
            {
                { "", "Value" }
            },
            Payload = Array.Empty<byte>()
        };

        // Act & Assert - Empty header names should be rejected
        var ex = Assert.ThrowsException<ArgumentException>(() => _codec.Encode(message));
        StringAssert.Contains(ex.Message, "Header name");
    }

    #endregion

    #region Decode Validation Tests

    [TestMethod]
    public void Decode_NullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => _codec.Decode(null!));
    }

    [TestMethod]
    public void Decode_EmptyData_ThrowsInvalidDataException()
    {
        // Arrange
        var emptyData = Array.Empty<byte>();

        // Act & Assert
        Assert.ThrowsException<InvalidDataException>(() => _codec.Decode(emptyData));
    }

    [TestMethod]
    public void Decode_IncompleteData_ThrowsInvalidDataException()
    {
        // Arrange - only header count, missing everything else
        var incompleteData = new byte[] { 0x01 }; // Says 1 header but no data

        // Act & Assert
        Assert.ThrowsException<InvalidDataException>(() => _codec.Decode(incompleteData));
    }

    [TestMethod]
    public void Decode_TruncatedHeader_ThrowsInvalidDataException()
    {
        // Arrange - header count says 1, but header data is incomplete
        var truncatedData = new byte[] { 0x01, 0x05, 0x00 }; // Header count=1, name length=5, but no name

        // Act & Assert
        Assert.ThrowsException<InvalidDataException>(() => _codec.Decode(truncatedData));
    }

    [TestMethod]
    public void Decode_TruncatedPayload_ThrowsInvalidDataException()
    {
        // Arrange - payload length says 100 but only a few bytes present
        var data = new byte[] { 0x00, 0x64, 0x00, 0x00, 0x01, 0x02, 0x03 }; // 0 headers, payload length=100, only 3 bytes

        // Act & Assert
        Assert.ThrowsException<InvalidDataException>(() => _codec.Decode(data));
    }

    [TestMethod]
    public void Decode_TrailingBytes_ThrowsInvalidDataException()
    {
        // Arrange - valid message followed by extra bytes
        var validMessage = _codec.Encode(new Message
        {
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>()
        });

        var dataWithTrailing = new byte[validMessage.Length + 5];
        Array.Copy(validMessage, dataWithTrailing, validMessage.Length);
        dataWithTrailing[validMessage.Length] = 0xFF;

        // Act & Assert
        Assert.ThrowsException<InvalidDataException>(() => _codec.Decode(dataWithTrailing));
    }

    [TestMethod]
    public void Decode_InvalidHeaderCount_ThrowsInvalidDataException()
    {
        // Arrange - header count of 64 (exceeds max of 63)
        var invalidData = new byte[] { 0x40, 0x00, 0x00, 0x00 }; // Header count=64

        // Act & Assert
        Assert.ThrowsException<InvalidDataException>(() => _codec.Decode(invalidData));
    }

    #endregion

    #region Binary Format Tests

    [TestMethod]
    public void Encode_EmptyMessage_ProducesCorrectBinaryFormat()
    {
        // Arrange
        var message = new Message
        {
            Headers = new Dictionary<string, string>(),
            Payload = Array.Empty<byte>()
        };

        // Act
        var encoded = _codec.Encode(message);

        // Assert
        Assert.AreEqual(4, encoded.Length); // 1 byte header count + 3 bytes payload length
        Assert.AreEqual(0x00, encoded[0]); // 0 headers
        Assert.AreEqual(0x00, encoded[1]); // Payload length byte 1
        Assert.AreEqual(0x00, encoded[2]); // Payload length byte 2
        Assert.AreEqual(0x00, encoded[3]); // Payload length byte 3
    }

    [TestMethod]
    public void Encode_SingleHeader_ProducesCorrectBinaryFormat()
    {
        // Arrange
        var message = new Message
        {
            Headers = new Dictionary<string, string>
            {
                { "Key", "Val" }
            },
            Payload = Array.Empty<byte>()
        };

        // Act
        var encoded = _codec.Encode(message);

        // Assert
        Assert.AreEqual(0x01, encoded[0]); // 1 header
        Assert.AreEqual(0x03, encoded[1]); // Name length = 3 (little-endian low byte)
        Assert.AreEqual(0x00, encoded[2]); // Name length = 3 (little-endian high byte)
        Assert.AreEqual((byte)'K', encoded[3]);
        Assert.AreEqual((byte)'e', encoded[4]);
        Assert.AreEqual((byte)'y', encoded[5]);
        Assert.AreEqual(0x03, encoded[6]); // Value length = 3
        Assert.AreEqual(0x00, encoded[7]);
        Assert.AreEqual((byte)'V', encoded[8]);
        Assert.AreEqual((byte)'a', encoded[9]);
        Assert.AreEqual((byte)'l', encoded[10]);
        // Followed by 3 bytes of payload length (all zeros)
    }

    [TestMethod]
    public void Encode_PayloadLength_UsesLittleEndian()
    {
        // Arrange - create message with payload length 0x010203 (66051 bytes)
        var payloadSize = 0x010203;
        var message = new Message
        {
            Headers = new Dictionary<string, string>(),
            Payload = new byte[payloadSize]
        };

        // Act
        var encoded = _codec.Encode(message);

        // Assert - check that payload length is encoded in little-endian
        var payloadLengthOffset = 1; // Right after header count
        Assert.AreEqual(0x03, encoded[payloadLengthOffset]); // Low byte
        Assert.AreEqual(0x02, encoded[payloadLengthOffset + 1]); // Middle byte
        Assert.AreEqual(0x01, encoded[payloadLengthOffset + 2]); // High byte
    }

    #endregion

    #region Round-Trip Tests

    [TestMethod]
    public void RoundTrip_MultipleMessages_AllSucceed()
    {
        // Arrange
        var messages = new[]
        {
            new Message { Headers = new Dictionary<string, string>(), Payload = Array.Empty<byte>() },
            new Message { Headers = new Dictionary<string, string> { { "A", "B" } }, Payload = new byte[] { 1, 2, 3 } },
            new Message { Headers = new Dictionary<string, string> { { "X", "Y" }, { "Z", "W" } }, Payload = new byte[1000] }
        };

        // Act & Assert
        foreach (var original in messages)
        {
            var encoded = _codec.Encode(original);
            var decoded = _codec.Decode(encoded);

            Assert.AreEqual(original.Headers.Count, decoded.Headers.Count);
            Assert.AreEqual(original.Payload.Length, decoded.Payload.Length);
        }
    }

    #endregion
}
