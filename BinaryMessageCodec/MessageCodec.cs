using System.Text;

namespace BinaryMessageCodec;

public class MessageCodec : IMessageCodec
{
    private const int MaxHeaders = 63;
    private const int MaxHeaderNameLength = 1023;
    private const int MaxHeaderValueLength = 1023;
    private const int MaxPayloadLength = 256 * 1024;

    public byte[] Encode(Message message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        if (message.Headers?.Count > MaxHeaders)
            throw new ArgumentException($"Message has {message.Headers.Count} headers, but maximum is {MaxHeaders}", nameof(message));

        if (message.Headers != null)
        {
            foreach (var header in message.Headers)
            {
                ValidateHeader(header.Key, "Header name", MaxHeaderNameLength, allowEmpty: false);
                ValidateHeader(header.Value, "Header value", MaxHeaderValueLength, allowEmpty: true);
            }
        }

        if (message.Payload?.Length > MaxPayloadLength)
            throw new ArgumentException($"Payload size {message.Payload.Length} exceeds maximum of {MaxPayloadLength} bytes", nameof(message));

        using var stream = new MemoryStream();

        var headerCount = message.Headers?.Count ?? 0;
        stream.WriteByte((byte)headerCount);

        if (message.Headers != null)
        {
            foreach (var header in message.Headers)
            {
                WriteHeader(stream, header.Key, header.Value);
            }
        }

        var payloadLength = message.Payload?.Length ?? 0;
        WriteUInt24(stream, payloadLength);

        if (message.Payload != null && message.Payload.Length > 0)
        {
            stream.Write(message.Payload, 0, message.Payload.Length);
        }

        return stream.ToArray();
    }

    public Message Decode(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (data.Length == 0)
            throw new InvalidDataException("Data cannot be empty");

        using var stream = new MemoryStream(data);
        var message = new Message();

        try
        {
            var headerCount = stream.ReadByte();
            if (headerCount == -1)
                throw new InvalidDataException("Unexpected end of stream while reading header count");

            if (headerCount > MaxHeaders)
                throw new InvalidDataException($"Header count {headerCount} exceeds maximum of {MaxHeaders}");

            message.Headers = new Dictionary<string, string>();
            for (int i = 0; i < headerCount; i++)
            {
                var (name, value) = ReadHeader(stream);
                message.Headers[name] = value;
            }

            var payloadLength = ReadUInt24(stream);
            if (payloadLength > MaxPayloadLength)
                throw new InvalidDataException($"Payload length {payloadLength} exceeds maximum of {MaxPayloadLength}");

            if (payloadLength > 0)
            {
                var remainingBytes = stream.Length - stream.Position;
                if (remainingBytes < payloadLength)
                    throw new InvalidDataException($"Expected {payloadLength} payload bytes but only {remainingBytes} remain");

                message.Payload = new byte[payloadLength];
                var bytesRead = stream.Read(message.Payload, 0, payloadLength);
                if (bytesRead != payloadLength)
                    throw new InvalidDataException($"Could not read full payload: expected {payloadLength} bytes, got {bytesRead}");
            }
            else
            {
                message.Payload = Array.Empty<byte>();
            }

            if (stream.Position != stream.Length)
            {
                var trailingBytes = stream.Length - stream.Position;
                throw new InvalidDataException($"Unexpected {trailingBytes} trailing bytes after message");
            }

            return message;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("Failed to decode message", ex);
        }
    }


    private void WriteHeader(Stream stream, string name, string value)
    {
        var nameBytes = Encoding.ASCII.GetBytes(name);
        WriteUInt16(stream, (ushort)nameBytes.Length);
        stream.Write(nameBytes, 0, nameBytes.Length);

        var valueBytes = Encoding.ASCII.GetBytes(value);
        WriteUInt16(stream, (ushort)valueBytes.Length);
        stream.Write(valueBytes, 0, valueBytes.Length);
    }

    private (string name, string value) ReadHeader(Stream stream)
    {
        var nameLength = ReadUInt16(stream);
        if (nameLength > MaxHeaderNameLength)
            throw new InvalidDataException($"Header name length {nameLength} exceeds maximum of {MaxHeaderNameLength}");

        var nameBytes = new byte[nameLength];
        var bytesRead = stream.Read(nameBytes, 0, nameLength);
        if (bytesRead != nameLength)
            throw new InvalidDataException($"Could not read header name: expected {nameLength} bytes, got {bytesRead}");

        var name = Encoding.ASCII.GetString(nameBytes);

        var valueLength = ReadUInt16(stream);
        if (valueLength > MaxHeaderValueLength)
            throw new InvalidDataException($"Header value length {valueLength} exceeds maximum of {MaxHeaderValueLength}");

        var valueBytes = new byte[valueLength];
        bytesRead = stream.Read(valueBytes, 0, valueLength);
        if (bytesRead != valueLength)
            throw new InvalidDataException($"Could not read header value: expected {valueLength} bytes, got {bytesRead}");

        var value = Encoding.ASCII.GetString(valueBytes);

        return (name, value);
    }

    private void WriteUInt16(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)((value >> 8) & 0xFF));
    }

    private ushort ReadUInt16(Stream stream)
    {
        var b1 = stream.ReadByte();
        var b2 = stream.ReadByte();

        if (b1 == -1 || b2 == -1)
            throw new InvalidDataException("Unexpected end of stream while reading UInt16");

        return (ushort)(b1 | (b2 << 8));
    }

    private void WriteUInt24(Stream stream, int value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)((value >> 8) & 0xFF));
        stream.WriteByte((byte)((value >> 16) & 0xFF));
    }

    private int ReadUInt24(Stream stream)
    {
        var b1 = stream.ReadByte();
        var b2 = stream.ReadByte();
        var b3 = stream.ReadByte();

        if (b1 == -1 || b2 == -1 || b3 == -1)
            throw new InvalidDataException("Unexpected end of stream while reading UInt24");

        return b1 | (b2 << 8) | (b3 << 16);
    }

    private static void ValidateHeader(string value, string fieldName, int maxLength, bool allowEmpty = false)
    {
        if (value == null)
            throw new ArgumentException($"{fieldName} cannot be null");

        if (!allowEmpty && value.Length == 0)
            throw new ArgumentException($"{fieldName} cannot be empty");

        var bytes = Encoding.ASCII.GetBytes(value);
        var roundTrip = Encoding.ASCII.GetString(bytes);
        if (roundTrip != value)
            throw new ArgumentException($"{fieldName} must contain only ASCII characters");

        if (bytes.Length > maxLength)
            throw new ArgumentException($"{fieldName} length {bytes.Length} exceeds maximum of {maxLength} bytes");
    }
}
