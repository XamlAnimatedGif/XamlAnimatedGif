using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
#if WPF
using System.Windows.Media.Animation;
#elif WINRT
using Windows.UI.Xaml.Media.Animation;
#endif

namespace XamlAnimatedGif
{
    class TimingManager
    {
        private readonly RepeatBehavior _repeatBehavior;
        private readonly List<TimeSpan> _timeSpans = new List<TimeSpan>();
        private int _current;
        private int _count;
        private bool _isComplete;
        private TimeSpan _elapsed;

        public TimingManager(RepeatBehavior repeatBehavior)
        {
            _repeatBehavior = repeatBehavior;
        }

        public void Add(TimeSpan timeSpan)
        {
            _timeSpans.Add(timeSpan);
        }

        public async Task<bool> NextAsync(CancellationToken cancellationToken)
        {
            if (IsComplete)
                return false;

            await IsPausedAsync(cancellationToken);

            var ts = _timeSpans[_current];
            await Task.Delay(ts, cancellationToken);
            _current++;

            if (_repeatBehavior.HasDuration)
            {
                _elapsed += ts;
                if (_elapsed >= _repeatBehavior.Duration)
                {
                    IsComplete = true;
                    return false;
                }
            }

            if (_current >= _timeSpans.Count)
            {
                if (_repeatBehavior.HasCount)
                {
                    if (++_count < _repeatBehavior.Count)
                    {
                        _current = 0;
                        return true;
                    }
                    IsComplete = true;
                    return false;
                }
                else
                {
                    _current = 0;
                    return true;
                }
            }
            return true;
        }

        public void Reset()
        {
            _current = 0;
            _count = 0;
            _elapsed = TimeSpan.Zero;
            IsComplete = false;
        }

        public event EventHandler Completed;

        protected virtual void OnCompleted()
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }

        public bool IsComplete
        {
            get { return _isComplete; }
            private set
            {
                _isComplete = value;
                if (value)
                    OnCompleted();
            }
        }

        private readonly Task _completedTask = Task.FromResult(0);
        private bool _isPaused;
        private TaskCompletionSource<int> _pauseCompletionSource;
        public void Pause()
        {
            _isPaused = true;
            _pauseCompletionSource = new TaskCompletionSource<int>();
        }

        public void Resume()
        {
            var tcs = _pauseCompletionSource;
            if (tcs != null)
                tcs.TrySetResult(0);
            _pauseCompletionSource = null;
            _isPaused = false;
        }

        public bool IsPaused
        {
            get { return _isPaused; }
        }

        private Task IsPausedAsync(CancellationToken cancellationToken)
        {
            var tcs = _pauseCompletionSource;
            if (tcs != null)
            {
                if (cancellationToken.CanBeCanceled)
                    cancellationToken.Register(() => tcs.TrySetCanceled());
                return tcs.Task;
            }
            return _completedTask;
        }
    }
}