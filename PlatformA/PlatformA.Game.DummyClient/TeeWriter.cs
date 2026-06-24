namespace PlatformA.Game.DummyClient
{
    internal sealed class TeeWriter : TextWriter
    {
        private readonly TextWriter _console;
        private readonly StreamWriter _file;

        public TeeWriter(TextWriter console, StreamWriter file)
        {
            _console = console;
            _file = file;
        }

        public override System.Text.Encoding Encoding => _console.Encoding;

        public override void Write(char value)
        {
            _console.Write(value);
            _file.Write(value);
        }

        public override void WriteLine(string? value)
        {
            _console.WriteLine(value);
            _file.WriteLine(value);
            _file.Flush();
        }

        public override void WriteLine()
        {
            _console.WriteLine();
            _file.WriteLine();
            _file.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _file.Dispose();
            base.Dispose(disposing);
        }
    }
}
