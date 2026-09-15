module RoyalityTikTokSearcher.Handler.StartHandlerAsync

open System
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Telegram.Bot
open Telegram.Bot.Polling
open RoyalityTikTokSearcher.Config
open RoyalityTikTokSearcher.Handler

let startBotAsync () : Task =
    task {
        use client = new HttpClient()

        CollectorHandlerAsync.initialize client

        let botClient = TelegramBotClient telegramBotToken

        let! me = botClient.GetMe()

        printfn $"Бот @{me.Username} успешно запущен и готов к поиску в TikTok!"

        using (new CancellationTokenSource()) <| fun cts ->
            let receiverOptions = ReceiverOptions(AllowedUpdates = [||])

            let handler = 
                { new IUpdateHandler with
                    member _.HandleUpdateAsync(bot, update, token) = 
                        UpdateHandlerAsync.handleUpdateAsync bot update token
                
                    member _.HandleErrorAsync(bot, ex, source, token) = 
                        ErrorHandlerAsync.handleErrorAsync ex source }

            botClient.StartReceiving(handler, receiverOptions, cts.Token)

            printfn "Нажмите Enter в консоли, чтобы остановить бота..."
    
            Console.ReadLine() |> ignore
            
            cts.Cancel()
    }