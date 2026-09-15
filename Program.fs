open RoyalityTikTokSearcher.Handler.StartHandlerAsync

[<EntryPoint>]
let main argv =
    startBotAsync()
    |> fun task -> task.GetAwaiter().GetResult()
    0
