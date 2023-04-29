using System;
using System.Runtime.CompilerServices;

namespace HalfMaid.Async
{
	/// <summary>
	/// A GameTask represents a continuation of an interrupted method.  This version
	/// of GameTask represents a continuation that returns no data.
	/// </summary>
	[AsyncMethodBuilder(typeof(GameTaskBuilder))]
	public class GameTask
	{
		/// <summary>
		/// The current status of this task, either "in progress," or "succeeded"
		/// (finished without an exception) or "failed" (threw an exception before
		/// finishing).
		/// </summary>
		public GameTaskStatus Status { get; private set; }

		/// <summary>
		/// If an exception was thrown by this task, this is the exception.
		/// </summary>
		public Exception? Exception { get; private set; }

		/// <summary>
		/// A continuation to invoke to resume this task, if it is interrupted.
		/// </summary>
		private Action? _continuation;

		/// <summary>
		/// Notify this task that it has completed successfully, and trigger any
		/// registered continuation for subsequent tasks.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if the task has
		/// already completed.</exception>
		internal void Success()
		{
			if (Status != GameTaskStatus.InProgress)
				throw new InvalidOperationException("Cannot complete an already-finished task.");

			(Status, Exception) = (GameTaskStatus.Success, null);
			_continuation?.Invoke();
		}

		/// <summary>
		/// Notify this task that it has failed due to an exception, and trigger any
		/// registered continuation for subsequent tasks.
		/// </summary>
		/// <param name="exception">The uncaught exception that was thrown.</param>
		/// <exception cref="InvalidOperationException">Thrown if the task has
		/// already completed.</exception>
		internal void Fail(Exception exception)
		{
			if (Status != GameTaskStatus.InProgress)
				throw new InvalidOperationException("Cannot fail an already-finished task.");

			(Status, Exception) = (GameTaskStatus.Failed, exception);
			_continuation?.Invoke();
		}

		/// <summary>
		/// After this task completes, then immediately initiate the given subsequent task.
		/// </summary>
		/// <param name="task">The task to perform after this one.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Then(Func<GameTask> task)
		{
			SetContinuation(() => task());
		}

		/// <summary>
		/// Attach a continuation to this task, to be invoked when the task completes.
		/// If this task already has associated continuations, the new continuation
		/// will always execute after any continuations registered before it.
		/// </summary>
		/// <param name="continuation">The continuation to register.</param>
		internal void SetContinuation(Action continuation)
		{
			if (Status != GameTaskStatus.InProgress)
			{
				continuation?.Invoke();
				return;
			}

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
		/// Whether this task has completed or is still in progress.  This is required
		/// by the C# compiler.
		/// </summary>
		public bool IsCompleted
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Status != GameTaskStatus.InProgress;
		}

		/// <summary>
		/// Retrieve the awaiter that can register a continuation to fire
		/// when the task is interrupted.  This is required by the C# compiler.
		/// </summary>
		/// <returns>An awaiter that can register continuations with this task.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public GameTaskAwaiter GetAwaiter() => new GameTaskAwaiter(this);
	}

	/// <summary>
	/// A GameTask represents a continuation of an interrupted method.  This version
	/// of GameTask represents a continuation that returns an object of type T.
	/// </summary>
	[AsyncMethodBuilder(typeof(GameTaskBuilder<>))]
	public class GameTask<T> : GameTask
	{
		/// <summary>
		/// The result of the completed method, once it has completed.
		/// </summary>
		public T Result { get; private set; } = default!;

		/// <summary>
		/// This will be invoked once the method successfully completes, and
		/// will store the return value of the method where it can then be
		/// retrieved by the caller.
		/// </summary>
		/// <param name="result">The result of having successfully completed
		/// the method.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void Success(T result)
		{
			Result = result;
			Success();
		}

		/// <summary>
		/// Retrieve the awaiter that can register a continuation to fire
		/// when the task is interrupted.  This is required by the C# compiler.
		/// </summary>
		/// <returns>An awaiter that can register continuations with this task.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new GameTaskAwaiter<T> GetAwaiter() => new GameTaskAwaiter<T>(this);
	}
}
