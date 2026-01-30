MemeBot 🧠

MemeBot is a personal meme recommendation assistant. It doesn't just send random pictures — it analyzes the visual "vibe" of memes using neural networks and adapts to your specific sense of humor.

🚀 Key Features

Automated Collection: Background workers harvest content from Telegram channels (via MTProto) and popular subreddits (via RSS).

Visual Intelligence: Every image is processed through a CLIP model (vision_model.onnx), transforming it into a unique 512-dimensional vector (embedding) that represents its semantic meaning.

Similarity Search: Powered by Qdrant, the system finds memes similar to those you've liked and filters out patterns you dislike.

Smart Recommendations: The algorithm balances precise matching (80%) with random discovery (20%) to keep your feed fresh and engaging.

🛠 Tech Stack

Language: C# (.NET 8)

AI Model: CLIP (Vision Model) in ONNX format from Hugging Face.

Vector Database: Qdrant (Windows Executable).

Relational Database: SQL Server (EF Core) for tracking user reactions and state.

Telegram Integration: WTelegramClient (for scraping) and Telegram.Bot (for the user interface).

📦 Setup & Installation
1. Vector Database (Qdrant)

Download the Windows ZIP archive from the Qdrant Releases.

Run qdrant.exe.

Note: The built-in Web Dashboard may be unavailable in the Windows standalone version. You can verify the service is running by visiting http://localhost:6333/collections.

2. Neural Model

Download the vision_model.onnx (ViT-B/32) file and place it in the /LLMModels folder in the project root.

3. Configuration (appsettings.json)

Create your configuration file based on the template below:
```
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MemeSageDb;Trusted_Connection=True;"
  },
  "Telegram": {
    "ApiId": "YOUR_API_ID",
    "ApiHash": "YOUR_API_HASH",
    "PhoneNumber": "+1..."
  },
  "Bot": {
    "Token": "YOUR_BOT_TOKEN"
  },
  "Qdrant": {
    "Host": "localhost",
    "Port": 6334,
    "CollectionName": "memes"
  },
  "Reddit": {
    "TargetSubreddits": ["ProgrammerHumor", "cats", "dankmemes"],
    "RefreshIntervalMinutes": 10,
    "UserAgent": "pc:MemeBot:v1.0 (by /u/YourUsername)"
  },
  "Recommendations": {
    "MinLikesToStart": 1,
    "RandomFactorPercent": 20,
    "RandomPoolSize": 50
  }
}
```
4. Running the App

Run Update-Database in the Package Manager Console to set up SQL Server tables.

During the first run, the Telegram scraper will prompt you for a verification code directly in the console. The session will then be saved to a local .session file.

The application automatically creates the required Qdrant collection on startup.

## 🔗 Resources & Acknowledgments

- **[Qdrant](https://qdrant.tech/)** - The Vector Database for the next generation of AI.
- **[Hugging Face]** - The platform where the CLIP model was sourced.
(https://huggingface.co/](https://huggingface.co/Xenova/clip-vit-base-patch32/tree/main/onnx)
