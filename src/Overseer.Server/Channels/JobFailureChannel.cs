using Overseer.Server.Models;

namespace Overseer.Server.Channels;

public interface IJobFailureChannel : IChannelBase<JobFailureAnalysisResult>;

public class JobFailureChannel : ChannelBase<JobFailureAnalysisResult>, IJobFailureChannel { }
