using MemeGodBot.ConsoleApp.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace MemeGodBot.ConsoleApp.Helpers
{
    public class ClipTextProcessor : ITextEncoder, IDisposable
    {
        private readonly Tokenizer _tokenizer;
        private readonly InferenceSession _session;
        private const int MaxLength = 77; //CLIP стандарт

        public ClipTextProcessor(string modelPath, string vocabPath, string mergesPath)
        {
            using var vocabStream = File.OpenRead(vocabPath);
            using var mergesStream = File.OpenRead(mergesPath);

            _tokenizer = BpeTokenizer.Create(vocabStream, mergesStream);
            _session = new InferenceSession(modelPath);
        }

        public float[] GetTextEmbedding(string text)
        {
            var tokenIdsInt = _tokenizer.EncodeToIds(text);
            var tokenIds = tokenIdsInt.Select(x => (long)x).ToList();

            var inputIds = new long[MaxLength];
            var attentionMask = new long[MaxLength];

            long startToken = 49406;
            long endToken = 49407;

            int currentIdx = 0;

            inputIds[currentIdx] = startToken;
            attentionMask[currentIdx] = 1;
            currentIdx++;

            foreach (var id in tokenIds)
            {
                if (currentIdx >= MaxLength - 1)
                    break;

                inputIds[currentIdx] = id;
                attentionMask[currentIdx] = 1;
                currentIdx++;
            }

            inputIds[currentIdx] = endToken;
            attentionMask[currentIdx] = 1;

            var inputTensor = new DenseTensor<long>(inputIds, new[] { 1, MaxLength });
            var attentionTensor = new DenseTensor<long>(attentionMask, new[] { 1, MaxLength });

            var inputs = new List<NamedOnnxValue>();

            string inputIdsName = _session.InputMetadata.ContainsKey("input_ids") ? "input_ids" : _session.InputMetadata.Keys.First();
            inputs.Add(NamedOnnxValue.CreateFromTensor(inputIdsName, inputTensor));

            if (_session.InputMetadata.ContainsKey("attention_mask"))
                inputs.Add(NamedOnnxValue.CreateFromTensor("attention_mask", attentionTensor));

            using var results = _session.Run(inputs);

            var outputTensor = results.First(x => x.Value is Tensor<float>).AsTensor<float>();
            var rawVector = outputTensor.ToArray();

            return NormalizeVector(rawVector);
        }

        private float[] NormalizeVector(float[] vector)
        {
            float sumSquares = 0f;

            for (int i = 0; i < vector.Length; i++)
                sumSquares += vector[i] * vector[i];

            var norm = MathF.Sqrt(sumSquares);

            if (norm == 0)
                return vector;

            for (int i = 0; i < vector.Length; i++)
                vector[i] /= norm;

            return vector;
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}