using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace AvaloniaGif
{
    public partial class GifImage
    {
        internal class GifBackgroundWorker
        {
            private readonly GifRenderer _gifRenderer;
            private readonly CancellationToken _ctoken;
            private readonly Memory<TimeSpan> _frameTimes;
            private int _currentIndex = -1, _frameCount;
            private Task bgThread;

            private Command _command = Command.Null;
            private State _state = State.Null;
            private Mutex stateMutex = new Mutex();
            private ConcurrentQueue<Command> cmd_queue = new ConcurrentQueue<Command>();
            
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
                CancellationToken cts) : base()
            {
                this._gifRenderer = gifRenderer;
                this._ctoken = cts;
                this._frameTimes = frameTimes;

                _currentIndex = 0;
                _frameCount = _gifRenderer.FrameCount;
                bgThread = Task.Factory.StartNew(MainLoop, _ctoken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            }

            public void SendCommand(Command cmd)
            {
                cmd_queue.Enqueue(cmd);
            }

            volatile bool shouldStop = false;

            public State GetState()
            {
                State ret;
                stateMutex.WaitOne();
                ret = _state;
                stateMutex.ReleaseMutex();
                return ret;
            }

            private async void MainLoop()
            {
                while (true)
                {
                    if (_ctoken.IsCancellationRequested | shouldStop)
                    {
                        DoStop();
                        break;
                    }

                    if (cmd_queue.TryDequeue(out var cmd))
                        switch (cmd)
                        {
                            case Command.Stop:
                                DoStop();
                                break;

                            case Command.Start:
                                SetState(State.Start);
                                break;
                        }

                    if (_state != State.Null)
                    {
                        switch (_state)
                        {
                            case State.Start:
                                await ShowFirstFrame();
                                SetState(State.Running);
                                break;
                            case State.Running:
                                await WaitAndRenderNext();
                                break;
                        }
                    }
                }
            }

            private void SetState(State state)
            {
                stateMutex.WaitOne();
                _state = state;
                stateMutex.ReleaseMutex();
            }

            private void DoStop()
            {
                SetState(State.Stop);
                shouldStop = true;
                _gifRenderer.Dispose();
            }

            async Task ShowFirstFrame()
            {
                if (GetState() == State.Stop)
                    return;

                await _gifRenderer.RenderFrameAsync(0);
            }

            async Task WaitAndRenderNext()
            {
                _currentIndex = (_currentIndex + 1) % _frameCount;
                var v = Task.Delay(_frameTimes.Span[_currentIndex]);
                var k = _gifRenderer.RenderFrameAsync(_currentIndex);
                await Task.WhenAll(v, k);
            }

            ~GifBackgroundWorker()
            {
                DoStop();
            }
        }
    }
}