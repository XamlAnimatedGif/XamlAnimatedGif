// using System;
// using System.Collections.Generic;
// using System.Threading;
// using System.Threading.Tasks; 
// using Avalonia.Animation; 
// using TaskEx = System.Threading.Tasks.Task;

// namespace AvaloniaGif
// {
//     class TimingManager
//     {
//         private readonly List<TimeSpan> _timeSpans = new List<TimeSpan>();
//         private int _current;
//         private int _count;
//         private bool _isComplete;
//         private TimeSpan _elapsed;

//         public TimingManager(RepeatCount RepeatCount)
//         {
//             RepeatCount = RepeatCount;
//         }

//         public RepeatCount RepeatCount { get; set; }

//         public void Add(TimeSpan timeSpan)
//         {
//             _timeSpans.Add(timeSpan);
//         }

//         public async Task<bool> NextAsync(CancellationToken cancellationToken)
//         {
//             if (IsComplete)
//                 return false;

//             await IsPausedAsync(cancellationToken);

 
//             var ts = _timeSpans[_current];
//             await TaskEx.Delay(ts, cancellationToken);
//             _current++;
//             _elapsed += ts;
 

//             if (_current >= _timeSpans.Count)
//             {
//                 _count++;
//                 if (RepeatCount.HasCount)
//                 {
//                     if (_count < RepeatCount.Count)
//                     {
//                         _current = 0;
//                         return true;
//                     }
//                     IsComplete = true;
//                     return false;
//                 }
//                 else
//                 {
//                     _current = 0;
//                     return true;
//                 }
//             }
//             return true;
//         }

//         public void Reset()
//         {
//             _current = 0;
//             _count = 0;
//             _elapsed = TimeSpan.Zero;
//             IsComplete = false;
//         }

//         public event EventHandler Completed;

//         protected virtual void OnCompleted()
//         {
//             Completed?.Invoke(this, EventArgs.Empty);
//         }

//         public bool IsComplete
//         {
//             get { return _isComplete; }
//             private set
//             {
//                 _isComplete = value;
//                 if (value)
//                     OnCompleted();
//             }
//         }

//         private readonly Task _completedTask = TaskEx.FromResult(0);
//         private TaskCompletionSource<int> _pauseCompletionSource;
//         public void Pause()
//         {
//             IsPaused = true;
//             _pauseCompletionSource = new TaskCompletionSource<int>();
//         }

//         public void Resume()
//         {
//             var tcs = _pauseCompletionSource;
//             tcs?.TrySetResult(0);
//             _pauseCompletionSource = null;
//             IsPaused = false;
//         }

//         public bool IsPaused { get; private set; }

//         private Task IsPausedAsync(CancellationToken cancellationToken)
//         {
//             var tcs = _pauseCompletionSource;
//             if (tcs != null)
//             {
//                 if (cancellationToken.CanBeCanceled)
//                     cancellationToken.Register(() => tcs.TrySetCanceled());
//                 return tcs.Task;
//             }
//             return _completedTask;
//         }
//     }
// }