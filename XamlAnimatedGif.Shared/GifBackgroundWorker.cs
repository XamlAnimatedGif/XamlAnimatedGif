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
        private Task _bgThread;

        private State _state;

        private readonly Mutex _stateMutex = new Mutex();
        private readonly ConcurrentQueue<Command> _cmdQueue = new ConcurrentQueue<Command>();
        private volatile bool _shouldStop;
        private int _iterationCount;

        private static readonly Stopwatch _timer = Stopwatch.StartNew();

        private GifRepeatBehavior _repeatBehavior = new GifRepeatBehavior() { LoopForever = true };

        public GifRepeatBehavior RepeatCount
        {
            get => _repeatBehavior;
            set
            {
                _stateMutex.WaitOne();
                ResetPlayVars();
                _repeatBehavior = value;
                _stateMutex.ReleaseMutex();
            }
        }

        private void ResetPlayVars()
        {
            _iterationCount = 0;
            _currentIndex = -1;
        }

        public enum Command
        {
            Null,
            Play,
            Pause,
            Reset,
            Dispose
        }

        public enum State
        {
            Null,
            Start,
            Running,
            Paused,
            Complete,
            Dispose
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
                    DoDispose();
                    break;
                }

                _stateMutex.WaitOne();
                if (_cmdQueue.TryDequeue(out var cmd))
                    switch (cmd)
                    {
                        case Command.Dispose:
                            DoDispose();
                            break;
                        case Command.Play:
                            switch (_state)
                            {
                                case State.Null:
                                    _state = State.Start;
                                    break;
                                case State.Paused:
                                    _state = State.Running;
                                    break;
                                case State.Complete:
                                    ResetPlayVars();
                                    _state = State.Start;
                                    break;
                            }
                            break;
                        case Command.Pause:
                            switch (_state)
                            {
                                case State.Running:
                                    _state = State.Paused;
                                    break;
                            }
                            break;
                        case Command.Reset:
                            switch (_state)
                            {
                                case State.Paused:
                                case State.Complete:
                                case State.Running:
                                    ResetPlayVars();
                                    ShowFirstFrame();
                                    break;
                            }
                            break;
                    }
                _stateMutex.ReleaseMutex();


                switch (_state)
                {
                    case State.Null:
                    case State.Paused:
                        Thread.Sleep(60);
                        break;
                    case State.Start:
                        ShowFirstFrame();
                        SetState(State.Running);
                        break;
                    case State.Running:
                        WaitAndRenderNext();
                        break;
                    case State.Complete:
                        ResetPlayVars();
                        Thread.Sleep(60);
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

        private void DoDispose()
        {
            SetState(State.Dispose);
            _shouldStop = true;
            _gifDecode.Dispose();
        }

        private void ShowFirstFrame()
        {
            if (_shouldStop) return;
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
            DoDispose();
        }
    }
}