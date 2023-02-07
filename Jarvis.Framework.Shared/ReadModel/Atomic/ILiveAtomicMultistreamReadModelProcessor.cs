using System;
using System.Collections.Generic;
using System.Text;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
	/// <summary>
	/// Introduced with #44 this interface is an extension to <see cref="ILiveAtomicReadModelProcessor"/>
	/// that will allow to project more than one stream into one or more atomic readmodel. This is usually used
	/// to solve the situation where we need to project a series of stream up to a point in time with multiple
	/// readmodels.
	/// </summary>
	public interface ILiveAtomicMultistreamReadModelProcessor
	{
	}
}
