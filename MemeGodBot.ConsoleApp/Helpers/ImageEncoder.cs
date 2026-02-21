using MemeGodBot.ConsoleApp.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace MemeGodBot.ConsoleApp.Helpers
{
    public class ImageEncoder : IImageEncoder, IDisposable
    {
        private readonly InferenceSession _session;
        private const int ImageSize = 224;
        private const string InputLayerName = "pixel_values";

        // Константы нормализации (стандарт для CLIP)
        private static readonly float[] Mean = { 0.48145466f, 0.4578275f, 0.40821073f };
        private static readonly float[] Std = { 0.26862954f, 0.26130258f, 0.27577711f };

        public ImageEncoder(string modelPath)
        {
            _session = new InferenceSession(modelPath);
        }

        public float[] GenerateEmbedding(string imagePath)
        {
            using var image = LoadAndResizeImage(imagePath);

            var inputTensor = ConvertToTensor(image);
            var rawVector = RunInference(inputTensor);

            return NormalizeVector(rawVector);
        }

        private Image<Rgb24> LoadAndResizeImage(string path)
        {
            var image = Image.Load<Rgb24>(path);

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(ImageSize, ImageSize),
                Mode = ResizeMode.Crop
            }));

            return image;
        }

        private DenseTensor<float> ConvertToTensor(Image<Rgb24> image)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, ImageSize, ImageSize });

            image.ProcessPixelRows(accessor =>
            {
                float meanR = Mean[0], meanG = Mean[1], meanB = Mean[2];
                float stdR = Std[0], stdG = Std[1], stdB = Std[2];

                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);

                    for (int x = 0; x < accessor.Width; x++)
                    {
                        // Нормализуем каждый канал (R, G, B)
                        tensor[0, 0, y, x] = (row[x].R / 255f - meanR) / stdR;
                        tensor[0, 1, y, x] = (row[x].G / 255f - meanG) / stdG;
                        tensor[0, 2, y, x] = (row[x].B / 255f - meanB) / stdB;
                    }
                }
            });

            return tensor;
        }

        private float[] RunInference(DenseTensor<float> inputTensor)
        {
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(InputLayerName, inputTensor)
            };

            using var results = _session.Run(inputs);

            return results.First().AsEnumerable<float>().ToArray();
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