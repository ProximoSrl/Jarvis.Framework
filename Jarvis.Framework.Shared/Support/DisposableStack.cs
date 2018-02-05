using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Support
{
	/// <summary>
	/// Needed for serilog, it will accumulate disposable object to 
	/// dispose a list of object in the reverse order of addition.
	/// </summary>
	public class DisposableStack : IDisposable
	{
		private Stack<IDisposable> _stack;
		private Stack<IDisposable> Stack => _stack ?? (_stack = new Stack<IDisposable>());

		public void Push(IDisposable disposable)
		{
			Stack.Push(disposable);
		}

		public void Dispose()
		{
			while (_stack?.Count > 0)
			{
				_stack.Pop().Dispose();
			}
		}
	}
}
