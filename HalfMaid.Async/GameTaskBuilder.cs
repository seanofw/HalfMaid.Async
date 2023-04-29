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
		/// The task that is being built, lazily-constructed as needed.
		/// </summary>
		public GameTask Task
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _task ??= new GameTask();
		}
		private GameTask? _task;

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
		/// Notify the builder that the task has failed with an exception.
		/// </summary>
		/// <param name="exception">The uncaught exception that was thrown by the
		/// task's state machine.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetException(Exception exception)
			=> Task.Fail(exception);

		/// <summary>
		/// Notify the builder that the task has completed successfully.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetResult()
			=> Task.Success();

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
	public class GameTaskBuilder<T>
	{
		/// <summary>
		/// The task that is being built, lazily-constructed as needed.
		/// </summary>
		public GameTask<T> Task
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _task ??= new GameTask<T>();
		}
		private GameTask<T>? _task;

		/// <summary>
		/// Construct an instance of the GameTaskBuilder.  As the GameTaskBuilder
		/// is a struct, this effectively just returns a single pointer.
		/// </summary>
		/// <returns>The new GameTaskBuilder instance.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static GameTaskBuilder<T> Create() => new GameTaskBuilder<T>();

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
		/// Notify the builder that the task has failed with an exception.
		/// </summary>
		/// <param name="exception">The uncaught exception that was thrown by the
		/// task's state machine.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetException(Exception exception)
			=> Task.Fail(exception);

		/// <summary>
		/// Notify the builder that the task has completed successfully.
		/// </summary>
		/// <param name="result">The return value of the task's state machine, to be
		/// stored in the task so that it can be retrieved by the caller.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetResult(T result)
			=> Task.Success(result);

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
}
