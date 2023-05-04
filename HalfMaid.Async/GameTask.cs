using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace HalfMaid.Async
{
	/// <summary>
	/// A GameTask, which represents cooperative execution of an operation inside a
	/// single thread in a video-game engine.
	/// </summary>
	[AsyncMethodBuilder(typeof(GameTask))]
	public class GameTask
	{
		/// <summary>
		/// Return the task, which is this object.  This is required by the C# compiler.
		/// </summary>
		/// <remarks>
		/// Due to this bug --
		/// https://stackoverflow.com/questions/69396896/why-does-my-async-method-builder-have-to-be-a-class-or-run-in-debug-mode/69397857#69397857
		/// we combine the builder and the task together.  If we *have* to have
		/// data on the heap, the least we can do is keep it to a single object.
		/// </remarks>
		public GameTask Task
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this;
		}

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
		/// The dispatch information from the exception thrown by this task.
		/// </summary>
		public ExceptionDispatchInfo? ExceptionDispatchInfo { get; private set; }

		/// <summary>
		/// The captured execution context.  Assigned when the task is interrupted, and used
		/// to restore the execution context back to "normal" when the task is resumed.
		/// </summary>
		internal ExecutionContext? ExecutionContext { get; set; }

		/// <summary>
		/// A continuation to invoke after this task completes.
		/// </summary>
		private Action? _continuation;

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
		/// Construct an instance of the GameTask, which is also its own builder.  This is
		/// required by the C# compiler.  It cannot be inlined because otherwise
		/// ExecutionContext.Capture() might return the wrong context.
		/// </summary>
		/// <returns>The new GameTask instance.</returns>
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static GameTask Create()
		{
			GameTask task = new GameTask();
			task.ExecutionContext = ExecutionContext.Capture();
			return task;
		}

		/// <summary>
		/// Start the state machine of the task at its beginning.  Does nothing
		/// more than invoke `MoveNext()` on the state machine.  This is
		/// required by the C# compiler.
		/// </summary>
		/// <typeparam name="TStateMachine">The type of the state machine to initiate.</typeparam>
		/// <param name="stateMachine">A reference to the state machine itself.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Start<TStateMachine>(ref TStateMachine stateMachine)
			where TStateMachine : IAsyncStateMachine
			=> stateMachine.MoveNext();

		/// <summary>
		/// Set the state machine for this task builder.  This is a no-op.  This is
		/// required by the C# compiler.
		/// </summary>
		/// <param name="stateMachine">A reference to the state machine this task
		/// builder is managing.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetStateMachine(IAsyncStateMachine stateMachine)
		{
		}

		/// <summary>
		/// Notify this task that it has failed due to an exception, and trigger any
		/// registered continuation for subsequent tasks.  This is required by the
		/// C# compiler.
		/// </summary>
		/// <param name="exception">The uncaught exception that was thrown.</param>
		/// <exception cref="InvalidOperationException">Thrown if the task has
		/// already completed.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
		[StackTraceHidden]
#endif
		[DebuggerHidden]
		public void SetException(Exception exception)
		{
			if (Status != GameTaskStatus.InProgress)
				throw new InvalidOperationException("Cannot fail an already-finished task.");

			(Status, Exception, ExceptionDispatchInfo) = (GameTaskStatus.Failed, exception, ExceptionDispatchInfo.Capture(exception));
			Continue();
		}

		/// <summary>
		/// Notify this task that it has completed successfully, and trigger any
		/// registered continuation for subsequent tasks.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if the task has
		/// already completed.</exception>
#if NET6_0_OR_GREATER
		[StackTraceHidden]
#endif
		[DebuggerHidden]
		public void SetResult()
		{
			if (Status != GameTaskStatus.InProgress)
				throw new InvalidOperationException("Cannot complete an already-finished task.");

			Status = GameTaskStatus.Success;
			Continue();
		}

		/// <summary>
		/// Use the current continuation to continute the task in the correct execution context.
		/// </summary>
#if NET6_0_OR_GREATER
		[StackTraceHidden]
#endif
		[DebuggerHidden]
		internal void Continue()
		{
			if (_continuation == null)
				return;

			if (ExecutionContext != null)
				ExecutionContext.Run(ExecutionContext, ExecutionContextRunner, _continuation);
			else
				_continuation();
		}

		/// <summary>
		/// Run the given provided callback inside a custom execution context.
		/// </summary>
		/// <param name="obj">The callback to invoke, which must be an Action.</param>
#if NET6_0_OR_GREATER
		[StackTraceHidden]
#endif
		[DebuggerHidden]
		private static void ExecutionContextRunner(object? obj)
		{
			((Action)obj!)();
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
				_continuation = continuation;
				Continue();
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
		/// Tell the given awaiter how to continue after its wait completes, which is
		/// to invoke the next phase of the given state machine.
		/// </summary>
		/// <typeparam name="TAwaiter">The type of the awaiter that will need to perform
		/// more work after it completes.</typeparam>
		/// <typeparam name="TStateMachine">The type of the state machine that needs to
		/// be continued by the awaiter.</typeparam>
		/// <param name="awaiter">The awaiter that will need to perform more work
		/// after it completes.</param>
		/// <param name="stateMachine">The state machine that needs to be continued
		/// by the awaiter.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AwaitOnCompleted<TAwaiter, TStateMachine>(
			ref TAwaiter awaiter, ref TStateMachine stateMachine)
			where TAwaiter : INotifyCompletion
			where TStateMachine : IAsyncStateMachine
		{
			awaiter.OnCompleted(stateMachine.MoveNext);
		}

		/// <summary>
		/// Tell the given awaiter how to continue after its wait completes, which is
		/// to invoke the next phase of the given state machine.  The unsafe version does
		/// not switch environments, but GameTasks should always be run within their
		/// original thread anyway, so this effectively does the same thing that
		/// AwaitOnCompleted() does.
		/// </summary>
		/// <typeparam name="TAwaiter">The type of the awaiter that will need to perform
		/// more work after it completes.</typeparam>
		/// <typeparam name="TStateMachine">The type of the state machine that needs to
		/// be continued by the awaiter.</typeparam>
		/// <param name="awaiter">The awaiter that will need to perform more work
		/// after it completes.</param>
		/// <param name="stateMachine">The state machine that needs to be continued
		/// by the awaiter.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
			ref TAwaiter awaiter, ref TStateMachine stateMachine)
			where TAwaiter : ICriticalNotifyCompletion
			where TStateMachine : IAsyncStateMachine
		{
			awaiter.UnsafeOnCompleted(stateMachine.MoveNext);
		}

		/// <summary>
		/// Retrieve the awaiter that can register a continuation to fire
		/// when the task is interrupted.  This is required by the C# compiler.
		/// </summary>
		/// <returns>An awaiter that can register continuations with this task.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public GameTaskAwaiter GetAwaiter()
			=> new GameTaskAwaiter(this);
	}

	/// <summary>
	/// A GameTask that returns a value.
	/// </summary>
	[AsyncMethodBuilder(typeof(GameTask<>))]
	public class GameTask<T> : GameTask
	{
		/// <summary>
		/// Return the task, which is this object.  This is required by the C# compiler.
		/// </summary>
		/// <remarks>
		/// Due to this bug --
		/// https://stackoverflow.com/questions/69396896/why-does-my-async-method-builder-have-to-be-a-class-or-run-in-debug-mode/69397857#69397857
		/// we combine the builder and the task together.  If we *have* to have
		/// data on the heap, the least we can do is keep it to a single object.
		/// </remarks>
		public new GameTask<T> Task
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this;
		}

		/// <summary>
		/// The result of the completed method, once it has completed.
		/// </summary>
		public T Result { get; private set; } = default!;

		/// <summary>
		/// Construct an instance of the GameTask, which is also its own builder.  This
		/// is required by the C# compiler.  It cannot be inlined because otherwise
		/// ExecutionContext.Capture() might return the wrong context.
		/// </summary>
		/// <returns>The new GameTaskBuilder instance.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static GameTask<T> Create() => new GameTask<T>();

		/// <summary>
		/// Notify the builder that the task has completed successfully.  This is required
		/// by the C# compiler.
		/// </summary>
		/// <param name="result">The return value of the task's state machine, to be
		/// stored in the task so that it can be retrieved by the caller.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetResult(T result)
		{
			Result = result;
			base.SetResult();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new GameTaskAwaiter<T> GetAwaiter()
			=> new GameTaskAwaiter<T>(this);

		#region Forwarded methods

		// The compiler requires all of these to exist *on* this class, but we don't
		// actually need them to be different than the base, so we just explicitly
		// forward them to the base and let the JIT figure it out.

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new void Start<TStateMachine>(ref TStateMachine stateMachine)
			where TStateMachine : IAsyncStateMachine
			=> base.Start(ref stateMachine);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new void SetStateMachine(IAsyncStateMachine stateMachine)
			=> base.SetStateMachine(stateMachine);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new void SetException(Exception exception)
			=> base.SetException(exception);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new void AwaitOnCompleted<TAwaiter, TStateMachine>(
			ref TAwaiter awaiter, ref TStateMachine stateMachine)
			where TAwaiter : INotifyCompletion
			where TStateMachine : IAsyncStateMachine
			=> base.AwaitOnCompleted(ref awaiter, ref stateMachine);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
			ref TAwaiter awaiter, ref TStateMachine stateMachine)
			where TAwaiter : ICriticalNotifyCompletion
			where TStateMachine : IAsyncStateMachine
			=> base.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);

		#endregion
	}
}
