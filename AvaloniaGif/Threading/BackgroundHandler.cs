

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace AvaloniaGif.Threading
{
    public class BackgroundHandler<TMessage> where TMessage : Enum
    {
        ConcurrentQueue<TMessage> _messageQueue;
        ConcurrentDictionary<TMessage, Action> _messageHandlers;
        CancellationTokenSource _cts;
        Task bgThread;
        object stopLock = new object();

        SemaphoreSlim semaphore = new SemaphoreSlim(1);
        TMessage taskMessage;

        public BackgroundHandler()
        {
            _cts = new CancellationTokenSource();
            _messageQueue = new ConcurrentQueue<TMessage>();
            _messageHandlers = new ConcurrentDictionary<TMessage, Action>();
        }

        private void MainLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                if (_messageQueue.TryDequeue(out var message))
                    if (_messageHandlers.TryGetValue(message, out var action))
                        action();

                


                Thread.Sleep(1);
            }
        }

        public void AddHandlers(params (TMessage message, Action action)[] handlers)
        {
            foreach (var handler in handlers)
                _messageHandlers.AddOrUpdate(handler.message, handler.action, (m, oa) => handler.action);
        }

        protected void SetTaskStatus(TMessage message)
        {
            semaphore.Wait();
            taskMessage = message;
            semaphore.Release();
        }

        public TMessage GetTaskStatus()
        {
            semaphore.Wait();
            var lastMessage = taskMessage;
            semaphore.Release();
            return lastMessage;
        }

        public void Start()
        {
            bgThread = Task.Factory.StartNew(MainLoop, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Stop()
        {
            _cts.Cancel();
        }

        public void SendMessage(TMessage message)
        {
            _messageQueue.Enqueue(message);
        }
    }
}