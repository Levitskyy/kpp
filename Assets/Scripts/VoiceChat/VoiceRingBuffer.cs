public class VoiceRingBuffer
{
    private int _capacity;
    private float[] _buffer;
    private object _lock = new object();
    private int _writeHead = 0;
    private int _readHead = 0;
    private int _availableSamples = 0;

    // how much samples accumulate before starting to give it away
    private readonly int _targetLatencySamples;
    // cap at which latency grown too big so we fast forward to fresh samples
    private readonly int _maxLatencySamples;

    // shows if we got enough samples to start giving them away
    private bool _isAccumulated;
    // use it to fade out at the end of audio
    private float _lastSample;

    public VoiceRingBuffer(int sampleRate, int targetLatencyMs = 60, int maxLatencyMs = 200)
    {
        _targetLatencySamples = sampleRate * targetLatencyMs / 1000;
        _maxLatencySamples = sampleRate * maxLatencyMs / 1000;
        _capacity = _maxLatencySamples * 2;
        _buffer = new float[_capacity];
    }

    public void Write(float[] source, int count)
    {
        lock (_lock)
        {
            for (int i = 0; i < count; ++i)
            {
                _buffer[_writeHead] = source[i];
                _writeHead = (_writeHead + 1) % _capacity;
                if (_availableSamples < _capacity)
                {
                    ++_availableSamples;
                }
                else
                {
                    // not sure if this could even happen but still
                    // if buffer is overloaded then we drop oldest samples
                    _readHead = (_readHead + 1) % _capacity;
                }
            }
            // fast forward to fresh samples if we built up too much latency
            if (_availableSamples > _maxLatencySamples)
            {
                int drop = _availableSamples - _targetLatencySamples;
                _readHead = (_readHead + drop) % _capacity;
                _availableSamples -= drop;
            }
        }
    }

    public void Read(float[] destination, int count)
    {
        lock (_lock)
        {
            if (!_isAccumulated)
            {
                if (_availableSamples < _targetLatencySamples)
                {
                    FillSilence(destination, count);
                    return;
                }
                _isAccumulated = true;
            }

            for (int i = 0; i < count; ++i)
            {
                if (_availableSamples > 0)
                {
                    float sample = _buffer[_readHead];
                    _readHead = (_readHead + 1) % _capacity;
                    _lastSample = sample;
                    --_availableSamples;
                    destination[i] = sample;
                }
                else
                {
                    // fading out last sound
                    _lastSample *= 0.9f;
                    destination[i] = _lastSample;

                    _isAccumulated = false;
                }
            }
        }
    }

    private void FillSilence(float[] destination, int count)
    {
        for (int i = 0; i < count; ++i)
        {
            destination[i] = 0f;
        }
    }

    public int BufferedSamplesCount
    {
        get { lock (_lock) return _availableSamples; }
    }
}