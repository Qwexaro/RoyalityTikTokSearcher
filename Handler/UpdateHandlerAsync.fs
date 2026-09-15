module RoyalityTikTokSearcher.Handler.UpdateHandlerAsync

open System
open System.IO
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open System.Text.Json.Nodes
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums
open RoyalityTikTokSearcher.Handler 

let private downloadVideoInMemoryAsync (url: string) =
    task {
        try
            let client = 
                match CollectorHandlerAsync.HttpClient with
                | Some client -> client
                
                | None -> new HttpClient()
                
            use request = new HttpRequestMessage(HttpMethod.Get, url)
            
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36")
            
            request.Headers.Add("Referer", "https://tiktok.com")

            let! response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
            
            if response.IsSuccessStatusCode then
                let memoryStream = new MemoryStream()
            
                do! response.Content.CopyToAsync(memoryStream)
            
                memoryStream.Position <- 0L
            
                return Some memoryStream
            else
                printfn $"[TikTok Защита]: Сервер вернул код {response.StatusCode} при попытке скачать медиа."
            
                return None
        with ex ->
            printfn $"[Ошибка скачивания]: {ex.Message}"
            
            return None
    }

let handleUpdateAsync (bot: ITelegramBotClient) (update: Update) (ct: CancellationToken) : Task =
    task {
        if not (isNull update.Message) && not (isNull update.Message.Text) then
            let message = update.Message
            
            let messageText = message.Text
            
            let chatId = ChatId message.Chat.Id

            match messageText with
            
            | text when text.StartsWith("/start") ->
                let! _ = bot.SendMessage(
                    chatId = chatId,
                
                    text = "Привет! Напиши мне любую тему (например, *котики*, *лайфхаки*, *машины*), и я найду ТОП-20 популярных видео из TikTok!",
                
                    parseMode = ParseMode.Markdown,
                
                    cancellationToken = ct
                )
                ()

            | _ ->
                let! _ = bot.SendMessage(chatId, $"Ищу лучшие видео по запросу: \"{messageText}\"...", cancellationToken = ct)
                
                try
                    let! videos = CollectorHandlerAsync.searchTikTokVideosAsync messageText
                    
                    if videos.Count = 0 then
                        let! _ = bot.SendMessage(chatId, "Ничего не найдено по этому запросу.", cancellationToken = ct)
            
                        ()
                    else
                        let mutable count = 0            

                        for video: JsonNode in videos |> Seq.truncate 20 do
                            let videoUrl = 
                                let play = video["play"]
                                
                                if not (isNull play) then play.GetValue<string>()
                                
                                else 
                                    let v = video["video"]
                                
                                    if not (isNull v) && not (isNull v["playAddr"]) then v["playAddr"].GetValue<string>()
                                
                                    else null

                            let title = 
                                let t = video["title"]
                                
                                if not (isNull t) then t.GetValue<string>()
                                
                                else
                                    let d = video["desc"]
                                
                                    if not (isNull d) then d.GetValue<string>() else "Без названия"

                            let views = 
                                let pc = video["play_count"]
                                
                                if not (isNull pc) then pc.GetValue<int>()
                                
                                else
                                    let s = video["statistics"]
                                
                                    if not (isNull s) && not (isNull s["play_count"]) then s["play_count"].GetValue<int>() else 0

                            let likes = 
                                let dc = video["digg_count"]
                                
                                if not (isNull dc) then dc.GetValue<int>()
                                
                                else
                                    let s = video["statistics"]
                                
                                    if not (isNull s) && not (isNull s["digg_count"]) then s["digg_count"].GetValue<int>() else 0

                            let author = 
                                let auth = video["author"]
                                
                                if not (isNull auth) then
                                    let uid = auth["unique_id"]
                                
                                    if not (isNull uid) then uid.GetValue<string>()
                                
                                    else
                                        let nick = auth["nickname"]
                                        if not (isNull nick) then nick.GetValue<string>() else "unknown"
                                else "unknown"

                            if not (String.IsNullOrEmpty(videoUrl)) then
                                let fullCaption =
                                    $"""
                                    👤 Автор: @{author}
                                    👁 Просмотры: {views.ToString("N0")}
                                    ❤️ Лайки: {likes.ToString("N0")}\n\n📝 {title}
                                    """
                                
                                let caption = 
                                    if fullCaption.Length > 1024 then fullCaption.Substring(0, 1020) + "..."
                                
                                    else fullCaption

                                try
                                    let! streamResult = downloadVideoInMemoryAsync videoUrl
                                    
                                    match streamResult with
                                    
                                    | None -> printfn $"[Пропуск]: Не удалось скачать файл для ролика автора @{author}"
                                    
                                    | Some videoStream ->
                                        use _stream = videoStream 
                                    
                                        let fileToSend = InputFileStream(videoStream, $"{author}_video.mp4")

                                        let! _ = bot.SendVideo(
                                            chatId = chatId,
                                    
                                            video = fileToSend,
                                    
                                            caption = caption,
                                    
                                            cancellationToken = ct
                                        )

                                        count <- count + 1
                                    
                                        do! Task.Delay(1500, ct)
                                
                                with ex -> printfn $"Ошибка отправки ролика: {ex.Message}"

                        let! _ = bot.SendMessage(chatId, $"Успешно отправлено видео: {count}.", cancellationToken = ct)
                        
                        ()
                with ex ->
                    printfn $"Глобальная ошибка: {ex.Message}"
             
                    let! _ = bot.SendMessage(chatId, "Произошла ошибка на сервере поиска.", cancellationToken = ct)
             
                    ()
    } :> Task