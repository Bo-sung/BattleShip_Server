using BattleShip.Common.Packets;
using System;
using System.Collections.Generic;

namespace BattleShip.Common.Network
{
    public class PacketDispatcher
    {
        private readonly Dictionary<PacketId, Action<IPacket>> _handlers
            = new Dictionary<PacketId, Action<IPacket>>();

        // 핸들러 등록
        public void Register<T>(PacketId packetId, Action<T> handler) where T : IPacket
        {
            _handlers[packetId] = (packet) => handler((T)packet);
        }

        // 패킷 → 등록된 핸들러 호출
        public void Dispatch(IPacket packet)
        {
            if (_handlers.TryGetValue(packet.PacketId, out var handler))
                handler(packet);
            else
                Console.WriteLine($"[Dispatcher] 핸들러 없음: {packet.PacketId}");
        }
    }
}
