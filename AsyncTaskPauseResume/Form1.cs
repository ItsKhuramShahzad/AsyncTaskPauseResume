namespace AsyncTaskPauseResume
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
        public class PauseTokenSource
        {
            bool _paused = false;
            bool _pauseRequested = false;

            TaskCompletionSource<bool> _resumeRequestTcs;
            TaskCompletionSource<bool> _pauseConfirmationTcs;

            readonly SemaphoreSlim _stateAsyncLock = new SemaphoreSlim(1);
            readonly SemaphoreSlim _pauseRequestAsyncLock = new SemaphoreSlim(1);

            public PauseToken Token { get { return new PauseToken(this); } }

            public async Task<bool> IsPaused(CancellationToken token = default(CancellationToken))
            {
                await _stateAsyncLock.WaitAsync(token);
                try
                {
                    return _paused;
                }
                finally
                {
                    _stateAsyncLock.Release();
                }
            }

            public async Task ResumeAsync(CancellationToken token = default(CancellationToken))
            {
                await _stateAsyncLock.WaitAsync(token);
                try
                {
                    if (!_paused)
                    {
                        return;
                    }

                    await _pauseRequestAsyncLock.WaitAsync(token);
                    try
                    {
                        var resumeRequestTcs = _resumeRequestTcs;
                        _paused = false;
                        _pauseRequested = false;
                        _resumeRequestTcs = null;
                        _pauseConfirmationTcs = null;
                        resumeRequestTcs.TrySetResult(true);
                    }
                    finally
                    {
                        _pauseRequestAsyncLock.Release();
                    }
                }
                finally
                {
                    _stateAsyncLock.Release();
                }
            }

            public async Task PauseAsync(CancellationToken token = default(CancellationToken))
            {
                await _stateAsyncLock.WaitAsync(token);
                try
                {
                    if (_paused)
                    {
                        return;
                    }

                    Task pauseConfirmationTask = null;

                    await _pauseRequestAsyncLock.WaitAsync(token);
                    try
                    {
                        _pauseRequested = true;
                        _resumeRequestTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        _pauseConfirmationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        pauseConfirmationTask = WaitForPauseConfirmationAsync(token);
                    }
                    finally
                    {
                        _pauseRequestAsyncLock.Release();
                    }

                    await pauseConfirmationTask;

                    _paused = true;
                }
                finally
                {
                    _stateAsyncLock.Release();
                }
            }

            private async Task WaitForResumeRequestAsync(CancellationToken token)
            {
                using (token.Register(() => _resumeRequestTcs.TrySetCanceled(), useSynchronizationContext: false))
                {
                    await _resumeRequestTcs.Task;
                }
            }

            private async Task WaitForPauseConfirmationAsync(CancellationToken token)
            {
                using (token.Register(() => _pauseConfirmationTcs.TrySetCanceled(), useSynchronizationContext: false))
                {
                    await _pauseConfirmationTcs.Task;
                }
            }

            internal async Task PauseIfRequestedAsync(CancellationToken token = default(CancellationToken))
            {
                Task resumeRequestTask = null;

                await _pauseRequestAsyncLock.WaitAsync(token);
                try
                {
                    if (!_pauseRequested)
                    {
                        return;
                    }
                    resumeRequestTask = WaitForResumeRequestAsync(token);
                    _pauseConfirmationTcs.TrySetResult(true);
                }
                finally
                {
                    _pauseRequestAsyncLock.Release();
                }

                await resumeRequestTask;
            }
        }

        // PauseToken - consumer side
        public class PauseToken
        {
            readonly PauseTokenSource _source;

            public PauseToken(PauseTokenSource source) { _source = source; }

            public Task<bool> IsPaused() { return _source.IsPaused(); }

            public Task PauseIfRequestedAsync(CancellationToken token = default(CancellationToken))
            {
                return _source.PauseIfRequestedAsync(token);
            }
        }

        // Basic usage

        public static async Task DoWorkAsync(PauseToken pause, CancellationToken token)
        {
            try
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    richTextBox1.AppendText("Before await pause.PauseIfRequestedAsync()\n");
                    await pause.PauseIfRequestedAsync();
                    richTextBox1.AppendText("After await pause.PauseIfRequestedAsync()\n");

                    await Task.Delay(1000);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Exception: {0}", e.ToString());
                throw;
            }
        }
        static PauseTokenSource pts;
        static async Task Test(CancellationToken token)
        {
            //var pts = new PauseTokenSource();
            //var task = DoWorkAsync(pts.Token, token);
             pts = new PauseTokenSource();
            var task = DoWorkAsync(pts.Token, token);
           

            while ( true)
            {
                token.ThrowIfCancellationRequested();
               // richTextBox1.AppendText("");
                richTextBox1.AppendText("Press enter to pause...\n");
                

                richTextBox1.AppendText("Before pause requested\n");
                await pts.PauseAsync();
                richTextBox1.AppendText("\nAfter pause requested, paused: " + await pts.IsPaused());

                richTextBox1.AppendText("Press enter to resume...\n");
               

                richTextBox1.AppendText("Before resume\n");
                await pts.ResumeAsync();
                richTextBox1.AppendText("After resume\n");
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            await Test(CancellationToken.None);
        }

        private async void button2_Click(object sender, EventArgs e)
        {
           await pts.PauseIfRequestedAsync();
        }

        private async void button3_Click(object sender, EventArgs e)
        {
               
               await pts.ResumeAsync();
        }
    }

    // PauseTokenSource
    

}