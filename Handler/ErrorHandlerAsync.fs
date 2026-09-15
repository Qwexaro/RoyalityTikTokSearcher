module RoyalityTikTokSearcher.Handler.ErrorHandlerAsync

open System
open System.Threading.Tasks
open Telegram.Bot.Polling

let handleErrorAsync (ex: Exception) (source: HandleErrorSource) =
    printfn $"[Telegram Error] From {source}: ex: {ex.Message}"
    
    Task.CompletedTask
    