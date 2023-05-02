using System.Runtime.CompilerServices;
using System;
using System.Threading;

namespace HalfMaid.Async
{
	/// <summary>
	/// An awaitable that yields the CPU until an external event triggers it to
	/// invoke its continuation on the next available frame.
	/// </summary>
	public class ExternalTaskAwaitable : INotifyCompletion
	{
		/// <summary>
		/// The runner that will be used to invoke this awaitable's future execution.
		/// </summary>
		public readonly GameTaskRunner Runner;

		/// <summary>
		/// The continuation that will eventually be triggered by this awaitable, if any.
		/// </summary>
		private Action? _continuation;

		/// <summary>
		/// The execution context in which to continue the current task.
		/// </summary>
		private ExecutionContext? _executionContext;

		/// <summary>
		/// Answer whether this awaitable has been completed yet.  By definition, if it
		/// exists at all, it has not.
		/// </summary>
		public bool IsCompleted { get; private set; }

		/// <summary>
		/// Construct a new awaitable that yields the CPU unil the start of a future frame.
		/// </summary>
		/// <param name="runner">The runner that will be used to invoke this awaitable's
		/// future execution.</param>
		/// <param name="frameCount">The number of frames that should elapse before this
		/// awaitable should continue.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ExternalTaskAwaitable(GameTaskRunner runner, ExecutionContext? context)
		{
			Runner = runner;
			_executionContext = context;
		}

		/// <summary>
		/// Retrieve the awaiter that can actually enqueue the wait continuation.  This
		/// is required by the C# compiler.
		/// </summary>
		/// <returns>A new </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ExternalTaskAwaitable GetAwaiter() => this;

		/// <summary>
		/// Register the given continuation for future execution once this awaitable
		/// completes.  This is invoked by the generated code from the C# compiler.
		/// </summary>
		/// <param name="continuation">The continuation to register.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void OnCompleted(Action continuation)
		{
			if (_continuation == null)
				_continuation = continuation;
			else
			{
				Action previousContinuation = _continuation;
				_continuation = () =>
				{
					previousContinuation();
					continuation();
				};
			}
		}

		/// <summary>
		/// Trigger this awaitable's continuation to start execution on the next frame.
		/// </summary>
		internal void Trigger()
		{
			IsCompleted = true;

			if (_continuation != null)
				Runner.EnqueueFuture(_continuation, _executionContext, 0);
		}

		/// <summary>
		/// Retrieve the result of having executed this.  This is invoked by generated
		/// code from the C# compiler.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void GetResult()
		{
		}
	}
}
