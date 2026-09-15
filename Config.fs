module RoyalityTikTokSearcher.Config

open System.IO
open Microsoft.Extensions.Configuration

let private config =
    ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
        .Build()
        
let telegramBotToken = config["Telegram:BotToken"]

let rapidApiKey = config["RapidApi:Key"]

let rapidApiHost = config["RapidApi:Host"]