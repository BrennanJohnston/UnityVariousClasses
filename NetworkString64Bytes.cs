using System;
using Unity.Collections;
using Unity.Netcode;

public class NetworkString64Bytes : INetworkSerializeByMemcpy, IEquatable<NetworkString64Bytes>, INetworkSerializable {
    private ForceNetworkSerializeByMemcpy<FixedString64Bytes> _info;

    public bool Equals(NetworkString64Bytes other) {
        return this.ToString().Equals(other.ToString());
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        if (serializer.IsReader) {
            FastBufferReader reader = serializer.GetFastBufferReader();
            reader.ReadValueSafe(out _info);
        } else if (serializer.IsWriter) {
            FastBufferWriter writer = serializer.GetFastBufferWriter();
            writer.WriteValueSafe(_info);
        }
        //serializer.SerializeValue(ref _info);
    }

    public override string ToString() {
        return _info.Value.ToString();
    }

    public static implicit operator string(NetworkString64Bytes s) => s.ToString();
    public static implicit operator NetworkString64Bytes(string s) => new NetworkString64Bytes() { _info = new FixedString64Bytes(s) };
}