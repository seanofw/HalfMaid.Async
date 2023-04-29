using System.Runtime.CompilerServices;

namespace HalfMaid.Async
{
	/// <summary>
	/// A simple base class for async game objects.  You don't *need* to use this,
	/// but it can make it easier to write async game code.
	/// </summary>
	public abstract class AsyncGameObjectBase
	{
		/// <summary>
		/// A runner that is managing and executing the GameTasks.  This is effectively
		/// part of your game's environment.
		/// </summary>
		public static GameTaskRunner Runner { get; set; } = new GameTaskRunner();

		/// <summary>
		/// Construct a new base object.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected AsyncGameObjectBase()
		{
		}

		/// <summary>
		/// Wait for the next frame to perform the actions after this.
		/// Note that `await Next()` is equivalent to `await Delay(1)`.
		/// </summary>
		/// <returns>An awaitable that waits for the next frame.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public GameTaskYieldAwaitable Next() => Runner.Next();

		/// <summary>
		/// Wait for the the given number of frames to perform the actions after this.
		/// Note that `await Next()` is equivalent to `await Delay(1)`.
		/// </summary>
		/// <returns>An awaitable that waits for the given number of frames.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public GameTaskYieldAwaitable Delay(int frames) => Runner.Delay(frames);
	}
}
