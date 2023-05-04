using System.Runtime.CompilerServices;
using System;
using System.Threading;
using System.Diagnostics;

namespace HalfMaid.Async
{
	/// <summary>
	/// An awaitable that yields the CPU for this task until the start of a future frame.
	/// This class is both the Awaitable and the Awaiter.
	/// </summary>
	public readonly struct GameTaskYieldAwaitable : INotifyCompletion
	{
		/// <summary>
		/// The runner that will be used to invoke this awaitable's future execution.
		/// </summary>
		public readonly GameTaskRunner Runner;

		/// <summary>
		/// The number of frames that should elapse before this awaitable should continue.
		/// </summary>
		public readonly int FrameCount;

		/// <summary>
		/// The execution context in which the continuation should continue after this
		/// yield has completed.
		/// </summary>
		private readonly ExecutionContext? _executionContext;

		/// <summary>
		/// Answer whether this awaitable has been completed yet.  By definition, if it
		/// exists at all, it has not.
		/// </summary>
		public bool IsCompleted
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => false;
		}

		/// <summary>
		/// Construct a new awaitable that yields the CPU unil the start of a future frame.
		/// </summary>
		/// <param name="runner">The runner that will be used to invoke this awaitable's
		/// future execution.</param>
		/// <param name="frameCount">The number of frames that should elapse before this
		/// awaitable should continue.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public GameTaskYieldAwaitable(GameTaskRunner runner, ExecutionContext? context, int frameCount)
		{
			Runner = runner;
			FrameCount = frameCount;
			_executionContext = context;
		}

		/// <summary>
		/// Retrieve the awaiter that can actually enqueue the wait continuation.  This
		/// is required by the C# compiler.
		/// </summary>
		/// <returns>A new </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public GameTaskYieldAwaitable GetAwaiter() => this;

		/// <summary>
		/// Register the given continuation for future execution once this awaitable
		/// completes.  This is invoked by the generated code from the C# compiler.
		/// </summary>
		/// <param name="continuation">The continuation to register.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void OnCompleted(Action continuation)
			=> Runner.EnqueueFuture(continuation, _executionContext, FrameCount);

		/// <summary>
		/// Retrieve the result of having executed this.  This is invoked by generated
		/// code from the C# compiler as well.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void GetResult()
		{
			if (Runner.Canceller != null)
			{
				Exception e = Runner.Canceller.Invoke();
				throw e;
			}
		}
	}
}
