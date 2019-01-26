using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using XamlAnimatedGif.Decoding;
using System.Linq;

namespace XamlAnimatedGif
{
    internal sealed class GifBackgroundWorker
    {
        private static readonly Stopwatch _timer = Stopwatch.StartNew();
        private GifDecoder _gifDecoder;

        private Task _bgThread;
        private State _state;
        private readonly object _lockObj;
        private readonly Queue<Command> _cmdQueue;
        private readonly List<ulong> _colorTableIDList;
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


        public Action CurrentFrameChanged;
        private int _currentIndex;
        private volatile bool _hasSeeked;

        public int CurrentFrameIndex
        {
            get => _currentIndex;
            set
            {
                if (value != _currentIndex)
                    lock (_lockObj)
                        InternalSeek(value, true);
            }
        }

        private void ResetPlayVars()
        {
            _iterationCount = 0;
            CurrentFrameIndex = -1;
        }

        public enum Command
        {
            Null,
            Play,
            Pause,
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


        private void RefreshColorTableCache()
        {
            foreach (var cacheID in _colorTableIDList)
                GifDecoder.GlobalColorTableCache.TryGetValue(cacheID, out var _);
        }

        internal void Seek(int value)
        {
            lock (_lockObj)
                InternalSeek(value, true);
        }

        private void InternalSeek(int value, bool isManual)
        {
            //_gifDecoder.ClearImage();

            //var queriedFrames = _gifDecoder
            //                  .Frames
            //                  .TakeWhile((x, i) => i <= value)
            //                  .Where((p, i) => p.FrameDisposalMethod != FrameDisposal.Restore)
            //                  .Select((x, i) => i);

            //foreach (var frameIndex in queriedFrames)
            //{
            //    _gifDecoder.RenderFrame(frameIndex);
            //}

            //_gifDecoder.RenderFrame(value);

            for (int fI = 0; fI <= value; fI++)
            {
                var targetFrame = _gifDecoder.Frames[fI];

                if (fI != value & targetFrame.FrameDisposalMethod == FrameDisposal.Restore)
                    continue;

                _gifDecoder.RenderFrame(fI);
            }

            _currentIndex = value;

            if (isManual)
            {
                if (_state == State.Complete)
                {
                    _state = State.Paused;
                    _iterationCount = 0;
                }

                _hasSeeked = true;

                CurrentFrameChanged?.Invoke();
            }
        }

        public GifBackgroundWorker(GifDecoder gifDecode)
        {
            _gifDecoder = gifDecode;
            _lockObj = new object();
            _repeatBehavior = new GifRepeatBehavior() { LoopForever = true };
            _cmdQueue = new Queue<Command>();

            _colorTableIDList = _gifDecoder.Frames
                                          .Where(p => p.IsLocalColorTableUsed)
                                          .Select(p => p.LocalColorTableCacheID)
                                          .ToList();

            if (_gifDecoder.Header.HasGlobalColorTable)
                _colorTableIDList.Add(_gifDecoder.Header.GlobalColorTableCacheID);

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
                    Thread.Sleep(40);
                    break;
                case State.Paused:
                    RefreshColorTableCache();
                    Thread.Sleep(60);
                    break;
                case State.Start:
                    _state = State.Running;
                    break;
                case State.Running:
                    WaitAndRenderNext();
                    break;
                case State.Complete:
                    RefreshColorTableCache();
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
            }

        }

        private void DoDispose()
        {
            _state = State.Dispose;
            _shouldStop = true;
            _gifDecoder.Dispose();
        }

        private void ShowFirstFrame()
        {
            if (_shouldStop) return;
            _gifDecoder.RenderFrame(0);
        }

        private void WaitAndRenderNext()
        {
            //if (_hasSeeked)
            //{
            //    InternalSeek(_currentIndex, false);
            //    _hasSeeked = false;
            //    return;
            //}

            if (!RepeatCount.LoopForever & _iterationCount > RepeatCount.Count)
            {
                _state = State.Complete;
                return;
            }

            _currentIndex = (_currentIndex + 1) % _gifDecoder.Frames.Count;

            CurrentFrameChanged?.Invoke();

            var targetDelay = _gifDecoder.Frames[_currentIndex].FrameDelay;

            var t1 = _timer.Elapsed;

            _gifDecoder.RenderFrame(_currentIndex);

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