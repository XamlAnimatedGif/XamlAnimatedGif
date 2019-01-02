using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace AvaloniaGif
{
    internal class GifBackgroundWorker
    {
        private readonly GifRenderer _gifRenderer;
        private readonly CancellationToken _token;
        private readonly Memory<TimeSpan> _frameTimes;
        private int _currentIndex = -1;
        private readonly int _frameCount;
        private Task _bgThread;

        private State _state = State.Null;
        private readonly Mutex _stateMutex = new Mutex();
        private readonly ConcurrentQueue<Command> _cmdQueue = new ConcurrentQueue<Command>();
        private volatile bool _shouldStop;
        
        private static readonly Stopwatch _timer = Stopwatch.StartNew();

        public enum Command
        {
            Null,
            Start,
            Pause,
            Stop,
            Refresh_Properties
        }

        public enum State
        {
            Null,
            Start,
            Running,
            Paused,
            Stop
        }

        public GifBackgroundWorker(
            GifRenderer gifRenderer,
            Memory<TimeSpan> frameTimes,
            CancellationToken cts) 
        {
            _gifRenderer = gifRenderer;
            _token = cts;
            _frameTimes = frameTimes;
            _frameCount = _gifRenderer.FrameCount;

            _bgThread = Task.Factory.StartNew(MainLoop, _token, TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void SendCommand(Command cmd)
        {
            _cmdQueue.Enqueue(cmd);
        }

        public State GetState()
        {
            _stateMutex.WaitOne();
            var ret = _state;
            _stateMutex.ReleaseMutex();
            return ret;
        }

        private void MainLoop()
        {
            while (true)
            {
                if (_token.IsCancellationRequested | _shouldStop)
                {
                    DoStop();
                    break;
                }

                if (_cmdQueue.TryDequeue(out var cmd))
                    switch (cmd)
                    {
                        case Command.Stop:
                            DoStop();
                            break;

                        case Command.Start:
                            SetState(State.Start);
                            break; 
                    }

                if (_state == State.Null) continue;
                
                switch (_state)
                {
                    case State.Start:
                        ShowFirstFrame();
                        SetState(State.Running);
                        break;
                    case State.Running:
                        WaitAndRenderNext();
                        break;
                }
            }
        }

        private void SetState(State state)
        {
            _stateMutex.WaitOne();
            _state = state;
            _stateMutex.ReleaseMutex();
        }

        private void DoStop()
        {
            SetState(State.Stop);
            _shouldStop = true;
            _gifRenderer.Dispose();
        }

        private void ShowFirstFrame()
        {
            if (GetState() == State.Stop)
                return;

            _gifRenderer.RenderFrame(0);
        }

        private void WaitAndRenderNext()
        {
            _currentIndex = (_currentIndex + 1) % _frameCount;
            var targetDelay = _frameTimes.Span[_currentIndex];
            
            var t1 = _timer.Elapsed;
            
            _gifRenderer.RenderFrame(_currentIndex);

            var t2 = _timer.Elapsed;
            var delta = t2 - t1;
            
            if(delta > targetDelay) return;
            Thread.Sleep(targetDelay - delta);
        }

        ~GifBackgroundWorker()
        {
            DoStop();
        }
    }
}