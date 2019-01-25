using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using XamlAnimatedGif.Decoding;

namespace XamlAnimatedGif
{
    internal sealed class GifBackgroundWorker
    {
        private static readonly Stopwatch _timer = Stopwatch.StartNew();
        private GifDecoder _gifDecode;
        private int _currentIndex;
        private Task _bgThread;
        private State _state;
        private readonly object _lockObj;
        private readonly Queue<Command> _cmdQueue;
        private volatile bool _shouldStop;
        private int _iterationCount;
        private GifRepeatBehavior _repeatBehavior;

        public GifRepeatBehavior RepeatCount
        {
            get => _repeatBehavior;
            set
            {
                lock (_lockObj)
                {
                    ResetPlayVars();
                    _repeatBehavior = value;
                }
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
            _lockObj = new object();
            _repeatBehavior = new GifRepeatBehavior() { LoopForever = true };
            _cmdQueue = new Queue<Command>();

            ResetPlayVars();

            _bgThread = Task.Factory.StartNew(MainLoop, CancellationToken.None, TaskCreationOptions.LongRunning,
                TaskScheduler.Current);
        }

        public void SendCommand(Command cmd)
        {
            lock (_lockObj)
                _cmdQueue.Enqueue(cmd);
        }

        public State GetState()
        {
            lock (_lockObj)
            {
                var ret = _state;
                return ret;
            }
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

                CheckCommands();
                DoStates();
            }
        }

        private void DoStates()
        {
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

        private void CheckCommands()
        {
            Command cmd;

            lock (_lockObj)
            {
                if (_cmdQueue.Count <= 0) return;
                cmd = _cmdQueue.Dequeue();
            }

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

        }

        private void SetState(State state)
        {
            lock (_lockObj)
                _state = state;
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