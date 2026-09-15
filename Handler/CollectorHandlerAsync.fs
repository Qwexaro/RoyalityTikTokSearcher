module RoyalityTikTokSearcher.Handler.CollectorHandlerAsync

open System
open System.Net.Http
open System.Net.Http.Json
open System.Text.Json.Nodes
open System.Threading.Tasks
open RoyalityTikTokSearcher.Config
open System.Collections.Generic

let mutable private httpClient: HttpClient option = None

let initialize = fun client -> httpClient <- Some client

let HttpClient = httpClient
    
let private sanitize (str: string) = str.Replace("\r", "").Replace("\n", "").Trim()

let private getPlayCount (node: JsonNode) =
    let directCount = node["play_count"]
    
    if not (isNull directCount) then directCount.GetValue<int>()
    
    else
        let stats = node["statistics"]
        
        if not (isNull stats) && not (isNull stats["play_count"]) then stats["play_count"].GetValue<int>()
        
        else 0
    
let searchTikTokVideosAsync (keyword: string) : Task<List<JsonNode>> =
    task {
        match httpClient with
        | None ->
            printfn "[ERROR]: Collector don't initialize"
            
            return ResizeArray<JsonNode>()

        | Some client ->
            let host = rapidApiHost |> sanitize
            
            let keyword = keyword |> sanitize |> Uri.EscapeDataString
            
            let url = $"https://{host}/feed/search?keywords={keyword}&count=20&cursor=0"
            
            match Uri.TryCreate(url, UriKind.Absolute) with
            | false, _ ->
                printfn $"[CRITICAL ERROR]: URL IS INVALID! GENERIC URL: '{url}'"
            
                return ResizeArray<JsonNode>()
            
            | true, validatedUri ->
                try
                    use request = new HttpRequestMessage(HttpMethod.Get, validatedUri)
                    
                    request.Headers.Add("x-rapidapi-key", rapidApiKey |> sanitize)
                    
                    request.Headers.Add("x-rapidapi-host", host)
                    
                    let! response = client.SendAsync(request)
                    
                    if response.IsSuccessStatusCode then
                        let! jsonResponse = response.Content.ReadFromJsonAsync<JsonNode>()
                        
                        if not (isNull jsonResponse) then
                            let json = jsonResponse.ToString()
                        
                            printfn $"[API RESPONSE]: %s{json.Substring(0, Math.Min(150, json.Length))}. . ."
                            
                        let data = if isNull jsonResponse then null else jsonResponse["data"]
                        
                        let (videos: JsonArray) =
                            if isNull data then null
                        
                            elif not (isNull data["videos"]) then data["videos"] :?> JsonArray
                        
                            else data :?> JsonArray

                        if isNull videos then return List<JsonNode>()
                        
                        else
                            let sortedList =
                                videos
                                |> Seq.cast<JsonNode>
                                |> Seq.filter (isNull >> not)
                                |> Seq.sortByDescending getPlayCount
                            
                            return List<JsonNode>(sortedList)
                    else
                        let! errBody = response.Content.ReadAsStringAsync()
                        
                        printfn $"[RapidAPI Ошибка]: Статус {response.StatusCode}. Ответ: {errBody}"
                        
                        return List<JsonNode>()
                        
                with ex ->
                    printfn $"[Критическая ошибка сбора]: {ex.Message}"
                    
                    return List<JsonNode>()
    }
