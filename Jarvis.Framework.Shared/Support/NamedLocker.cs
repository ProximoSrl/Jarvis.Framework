using System;
using System.Collections.Concurrent;

namespace Jarvis.Framework.Shared.Support
{
	/// <summary>
	/// Taken from here: http://johnculviner.com/achieving-named-lock-locker-functionality-in-c-4-0/
	/// and modified to not pollute dictinoary with too many elements.
	/// </summary>
	public class NamedLocker
	{
		private const Int32 DefaultModulus = 1000;

		/// <summary>
		/// Singleton Pattern.
		/// </summary>
		public static NamedLocker Instance { get; private set; }

		static NamedLocker()
		{
			Instance = new NamedLocker();
		}

		private NamedLocker()
		{
			for (int i = 0; i <= DefaultModulus; i++)
			{
				GetLock(i);
			}
		}

		private readonly ConcurrentDictionary<Int32, object> _lockDict =
			new ConcurrentDictionary<Int32, object>();

		/// <summary>
		/// Get a lock associated to a given string, to avoid polluting the dictionary
		/// with potentially hundreds of thousands of elements, we use only the last
		/// two char of the id to generate the key for the lock. This will cause 1% of
		/// unnecessary lock but it will avoid loss of performance for dictionary.
		/// </summary>
		/// <param name="identityValue"></param>
		/// <returns></returns>
		public object GetLock(Int64 identityValue)
		{
			int key = GetKey(identityValue);
			_lockDict.GetOrAdd(key, s => new object());
			return _lockDict[key];
		}

		/// <summary>
		/// run a short lock inline using a lambda
		/// </summary>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="identityValue"></param>
		/// <param name="body"></param>
		/// <returns></returns>
		public TResult RunWithLock<TResult>(Int64 identityValue, Func<TResult> body)
		{
			lock (GetLock(identityValue))
			{
				return body();
			}
		}

		/// <summary>
		/// run a short lock inline using a lambda
		/// </summary>
		/// <param name="identityValue"></param>
		/// <param name="body"></param>
		public void RunWithLock(Int64 identityValue, Action body)
		{
			lock (GetLock(identityValue))
			{
				body();
			}
		}

		/// <summary>
		/// Remove old lock.
		/// </summary>
		/// <param name="identityValue"></param>
		public void RemoveLock(Int64 identityValue)
		{
			object o;
			_lockDict.TryRemove(GetKey(identityValue), out o);
		}

		private Int32 GetKey(Int64 identityValue)
		{
			return (Int32)identityValue % DefaultModulus;
		}
	}
}
