using Overseer.Server.Integration.Machines;
using Overseer.Server.Models;

namespace Overseer.Server.Channels;

public interface IMachineStatusChannel : IChannelBase<MachineStatus>;

public class MachineStatusChannel : ChannelBase<MachineStatus>, IMachineStatusChannel { }
