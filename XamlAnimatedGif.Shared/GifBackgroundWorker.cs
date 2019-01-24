using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using XamlAnimatedGif.Decoding;

namespace XamlAnimatedGif
{
    internal sealed class GifBackgroundWorker
    {
        private GifDecoder _gifDecode;

        private int _currentIndex = -1;
        private readonly int _frameCount;
        private Task _bgThread;

        private State _state = State.Null;
        private readonly Mutex _stateMutex = new Mutex();
        private readonly ConcurrentQueue<Command> _cmdQueue = new ConcurrentQueue<Command>();
        private volatile bool _shouldStop;
        private static readonly Stopwatch _timer = Stopwatch.StartNew();
        private int _iterationCount;
        private GifRepeatCount _repeatCount = new GifRepeatCount() { LoopForever = true };
        public GifRepeatCount RepeatCount
        {
            get => _repeatCount;
            set => _repeatCount = value;
        }

        public enum Command
        {
            Null,
            Start,
            Pause,
            Resume,
            Stop,
            Reset
        }

        public enum State
        {
            Null,
            Start,
            Running,
            Paused,
            Stop,
            Complete
        }

        public GifBackgroundWorker(GifDecoder gifDecode)
        {
            _gifDecode = gifDecode;
            _bgThread = Task.Factory.StartNew(MainLoop, CancellationToken.None, TaskCreationOptions.LongRunning,
                TaskScheduler.Current);
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
                if (_shouldStop)
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

                        case Command.Pause:
                            if (GetState() == State.Running)
                                SetState(State.Paused);
                            break;

                        case Command.Resume:
                            if (GetState() == State.Paused)
                                SetState(State.Running);
                            break;

                        case Command.Reset:
                            if (GetState() == State.Running || GetState() == State.Complete)
                            {
                                _currentIndex = 0;
                                SetState(State.Paused);
                            }
                            break;
                    }

                if (_state == State.Null)
                {
                    Thread.Sleep(2);
                    continue;
                }

                switch (_state)
                {
                    case State.Start:
                        ShowFirstFrame();
                        SetState(State.Running);
                        break;
                    case State.Running:
                        WaitAndRenderNext();
                        break;
                    case State.Complete:
                    case State.Paused:
                        Thread.Sleep(30);
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
            _gifDecode.Dispose();
        }

        private void ShowFirstFrame()
        {
            if (GetState() == State.Stop)
                return;

            _gifDecode.RenderFrame(0);
        }

        private void WaitAndRenderNext()
        {
            if (!RepeatCount.LoopForever & _iterationCount > RepeatCount.Count)
            {
                SetState(State.Complete);
                return;
            }

            _currentIndex = (_currentIndex + 1) % _gifDecode.Frames.Count;
            var targetDelay = _gifDecode.Frames[_currentIndex].FrameDelay;

            var t1 = _timer.Elapsed;

            _gifDecode.RenderFrame(_currentIndex);

            var t2 = _timer.Elapsed;
            var delta = t2 - t1;

            if (delta > targetDelay) return;
            Thread.Sleep(targetDelay - delta);

            if (!RepeatCount.LoopForever & _currentIndex == 0)
                _iterationCount++;
        }

        ~GifBackgroundWorker()
        {
            DoStop();
        }
    }
}