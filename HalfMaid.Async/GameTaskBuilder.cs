using System;
using System.Runtime.CompilerServices;

namespace HalfMaid.Async
{
	/// <summary>
	/// A builder for GameTasks.  This is needed by the C# compiler to support
	/// async/await.
	/// </summary>
	public class GameTaskBuilder
	{
		/// <summary>
		/// The task, which is simply a reference to this builder, which actually holds the state.
		/// </summary>
		/// <remarks>
		/// Due to this bug --
		/// https://stackoverflow.com/questions/69396896/why-does-my-async-method-builder-have-to-be-a-class-or-run-in-debug-mode/69397857#69397857
		/// we store the state of the task not in the task itself, but here in the builder.
		/// Ideally, the task should be the class and this should be a struct, but we have
		/// no choice but to make it the other way around if we only want one object on the heap.
		/// </remarks>
		public GameTask Task
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new GameTask(this);
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
		/// A continuation to invoke to resume this task, if it is interrupted.
		/// </summary>
		private Action? _continuation;

		/// <summary>
		/// Construct an instance of the GameTaskBuilder.  As the GameTaskBuilder
		/// is a struct, this effectively just returns a single pointer.
		/// </summary>
		/// <returns>The new GameTaskBuilder instance.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static GameTaskBuilder Create() => new GameTaskBuilder();

		/// <summary>
		/// Start the state machine of the task at its beginning.  Does nothing
		/// more than invoke `MoveNext()` on the state machine.
		/// </summary>
		/// <typeparam name="TStateMachine">The type of the state machine to initiate.</typeparam>
		/// <param name="stateMachine">A reference to the state machine itself.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Start<TStateMachine>(ref TStateMachine stateMachine)
			where TStateMachine : IAsyncStateMachine
			=> stateMachine.MoveNext();

		/// <summary>
		/// Set the state machine for this task builder.  This is a no-op.
		/// </summary>
		/// <param name="stateMachine">A reference to the state machine this task
		/// builder is managing.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetStateMachine(IAsyncStateMachine stateMachine)
		{
		}

		/// <summary>
		/// Notify this task that it has failed due to an exception, and trigger any
		/// registered continuation for subsequent tasks.
		/// </summary>
		/// <param name="exception">The uncaught exception that was thrown.</param>
		/// <exception cref="InvalidOperationException">Thrown if the task has
		/// already completed.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetException(Exception exception)
		{
			if (Status != GameTaskStatus.InProgress)
				throw new InvalidOperationException("Cannot fail an already-finished task.");

			(Status, Exception) = (GameTaskStatus.Failed, exception);
			_continuation?.Invoke();
		}

		/// <summary>
		/// Notify this task that it has completed successfully, and trigger any
		/// registered continuation for subsequent tasks.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if the task has
		/// already completed.</exception>
		public void SetResult()
		{
			if (Status != GameTaskStatus.InProgress)
				throw new InvalidOperationException("Cannot complete an already-finished task.");

			(Status, Exception) = (GameTaskStatus.Success, null);
			_continuation?.Invoke();
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
	}

	/// <summary>
	/// A builder for GameTasks that return a value.  This is needed by the C# compiler
	/// to support async/await.
	/// </summary>
	public class GameTaskBuilder<T> : GameTaskBuilder
	{
		/// <summary>
		/// The task, which is simply a reference to this builder, which actually holds the state.
		/// </summary>
		/// <remarks>
		/// Due to this bug --
		/// https://stackoverflow.com/questions/69396896/why-does-my-async-method-builder-have-to-be-a-class-or-run-in-debug-mode/69397857#69397857
		/// we store the state of the task not in the task itself, but here in the builder.
		/// Ideally, the task should be the class and this should be a struct, but we have
		/// no choice but to make it the other way around if we only want one object on the heap.
		/// </remarks>
		public new GameTask<T> Task
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new GameTask<T>(this);
		}

		/// <summary>
		/// The result of the completed method, once it has completed.
		/// </summary>
		public T Result { get; private set; } = default!;

		/// <summary>
		/// Construct an instance of the GameTaskBuilder.  As the GameTaskBuilder
		/// is a struct, this effectively just returns a single pointer.
		/// </summary>
		/// <returns>The new GameTaskBuilder instance.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static GameTaskBuilder<T> Create() => new GameTaskBuilder<T>();

		/// <summary>
		/// Notify the builder that the task has completed successfully.
		/// </summary>
		/// <param name="result">The return value of the task's state machine, to be
		/// stored in the task so that it can be retrieved by the caller.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetResult(T result)
		{
			Result = result;
			base.SetResult();
		}

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
