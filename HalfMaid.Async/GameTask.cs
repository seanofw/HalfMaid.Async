using System;
using System.Runtime.CompilerServices;

namespace HalfMaid.Async
{
	/// <summary>
	/// A GameTask represents a continuation of an interrupted method.  This version
	/// of GameTask represents a continuation that returns no data.
	/// </summary>
	[AsyncMethodBuilder(typeof(GameTaskBuilder))]
	public struct GameTask
	{
		/// <summary>
		/// The builder that holds the actual state for this task.
		/// </summary>
		internal readonly GameTaskBuilder Builder;

		/// <summary>
		/// Get the current state of this task.
		/// </summary>
		public GameTaskStatus Status
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Builder.Status;
		}

		/// <summary>
		/// Construct a new GameTask that references the given task-builder state.
		/// </summary>
		/// <param name="builder">The task builder that holds the actual task state.</param>
		public GameTask(GameTaskBuilder builder)
			=> Builder = builder;

		/// <summary>
		/// After this task completes, then immediately initiate the given subsequent task.
		/// </summary>
		/// <param name="task">The task to perform after this one.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Then(Func<GameTask> task)
			=> Builder.SetContinuation(() => task());

		/// <summary>
		/// Whether this task has completed or is still in progress.  This is required
		/// by the C# compiler.
		/// </summary>
		public bool IsCompleted
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Builder.Status != GameTaskStatus.InProgress;
		}

		/// <summary>
		/// Retrieve the awaiter that can register a continuation to fire
		/// when the task is interrupted.  This is required by the C# compiler.
		/// </summary>
		/// <returns>An awaiter that can register continuations with this task.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public GameTaskAwaiter GetAwaiter() => new GameTaskAwaiter(Builder);
	}

	/// <summary>
	/// A GameTask represents a continuation of an interrupted method.  This version
	/// of GameTask represents a continuation that returns an object of type T.
	/// </summary>
	[AsyncMethodBuilder(typeof(GameTaskBuilder<>))]
	public struct GameTask<T>
	{
		/// <summary>
		/// The builder that holds the actual state for this task.
		/// </summary>
		internal readonly GameTaskBuilder<T> Builder;

		/// <summary>
		/// Get the current state of this task.
		/// </summary>
		public GameTaskStatus Status
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Builder.Status;
		}

		/// <summary>
		/// The result of the completed method, once it has completed.
		/// </summary>
		public T Result
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Builder.Result;
		}

		/// <summary>
		/// Construct a new GameTask that references the given task-builder state.
		/// </summary>
		/// <param name="builder">The task builder that holds the actual task state.</param>
		public GameTask(GameTaskBuilder<T> builder)
			=> Builder = builder;

		/// <summary>
		/// Allow explict promotion of a GameTask to a GameTask{T}, if that's what type
		/// it is under the hood.
		/// </summary>
		/// <param name="task">The GameTask to promote to a GameTask{T}.</param>
		/// <exception cref="InvalidCastException">Thrown if the task being promoted to
		/// GameTask{T} isn't actually a GameTask{T}.</exception>
		public static implicit operator GameTask<T>(GameTask task)
			=> task.Builder is GameTaskBuilder<T> builder
				? new GameTask<T>(builder)
				: throw new InvalidCastException($"Cannot convert this GameTask to GameTask<{typeof(T)}>.");

		/// <summary>
		/// Allow implict demotion of a GameTask{T} to a GameTask.
		/// </summary>
		/// <param name="task">The GameTask{T} to demote to a GameTask.</param>
		public static implicit operator GameTask(GameTask<T> task)
			=> new GameTask(task.Builder);

		/// <summary>
		/// After this task completes, then immediately initiate the given subsequent task.
		/// </summary>
		/// <param name="task">The task to perform after this one.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Then(Func<GameTask> task)
			=> Builder.SetContinuation(() => task());

		/// <summary>
		/// Whether this task has completed or is still in progress.  This is required
		/// by the C# compiler.
		/// </summary>
		public bool IsCompleted
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Builder.Status != GameTaskStatus.InProgress;
		}

		/// <summary>
		/// Retrieve the awaiter that can register a continuation to fire
		/// when the task is interrupted.  This is required by the C# compiler.
		/// </summary>
		/// <returns>An awaiter that can register continuations with this task.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public GameTaskAwaiter<T> GetAwaiter() => new GameTaskAwaiter<T>(Builder);
	}
}
