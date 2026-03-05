using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using EZParser.Models;

namespace EZParser.Services
{
    public class PlayStationStoreParser
    {
        private const string BaseUrl = "https://store.playstation.com/en-us/pages/browse";
        private readonly HttpClient _httpClient;
        private HashSet<string> _foundGameNames; // Для отслеживания уникальных игр

        public PlayStationStoreParser()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://store.playstation.com/");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<List<GameResult>> SearchAsync(string query, int maxPages = 1)
        {
            var results = new List<GameResult>();
            _foundGameNames = new HashSet<string>(); // Сбрасываем при новом поиске
            
            for (int page = 0; page < maxPages; page++)
            {
                try
                {
                    // Попробуем разные варианты URL для пагинации
                    string searchUrl = GetPagedUrl(query, page);
                    
                    Console.WriteLine($"Загрузка страницы {page + 1}: {searchUrl}");
                    
                    var htmlContent = await GetPageContentAsync(searchUrl);
                    
                    if (string.IsNullOrEmpty(htmlContent))
                    {
                        Console.WriteLine($"Страница {page + 1} пуста");
                        break;
                    }

                    var pageResults = ParsePage(htmlContent);
                    
                    // Фильтруем дубликаты
                    var uniqueResults = new List<GameResult>();
                    foreach (var result in pageResults)
                    {
                        if (!_foundGameNames.Contains(result.Name))
                        {
                            _foundGameNames.Add(result.Name);
                            uniqueResults.Add(result);
                        }
                    }
                    
                    Console.WriteLine($"Найдено на странице {page + 1}: {pageResults.Count} игр, уникальных: {uniqueResults.Count}");

                    if (uniqueResults.Count == 0)
                    {
                        // Если на странице нет новых игр, прекращаем поиск
                        Console.WriteLine("Новых игр не найдено, остановка");
                        break;
                    }

                    results.AddRange(uniqueResults);
                    
                    // Увеличиваем задержку между запросами
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при загрузке страницы {page + 1}: {ex.Message}");
                    break;
                }
            }

            return results;
        }

        private string GetPagedUrl(string query, int page)
        {
            var encodedQuery = Uri.EscapeDataString(query);
            
            // Пробуем разные варианты URL для пагинации
            if (page == 0)
            {
                return $"https://store.playstation.com/en-us/search/{encodedQuery}";
            }
            else
            {
                // Вариант 1: с параметром start (как у вас)
                string url1 = $"https://store.playstation.com/en-us/search/{encodedQuery}?start={page * 24}";
                
                // Вариант 2: с параметром offset
                string url2 = $"https://store.playstation.com/en-us/search/{encodedQuery}?offset={page * 24}";
                
                // Вариант 3: с параметром page
                string url3 = $"https://store.playstation.com/en-us/search/{encodedQuery}?page={page + 1}";
                
                // PlayStation может использовать другие параметры
                string url4 = $"https://store.playstation.com/en-us/search/{encodedQuery}?smd=true&start={page * 24}&limit=24";
                
                // Возвращаем первый вариант (можно менять для тестирования)
                return url1;
            }
        }

        private async Task<string> GetPageContentAsync(string url)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                
                // Проверяем, что страница содержит результаты
                if (content.Length < 1000 || content.Contains("No results found") || content.Contains("0 results"))
                    return null;

                return content;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки: {ex.Message}");
                return null;
            }
        }

        private List<GameResult> ParsePage(string htmlContent)
        {
            var results = new List<GameResult>();
            
            // Сохраняем HTML для отладки
            // File.WriteAllText($"debug_page_{DateTime.Now.Ticks}.html", htmlContent);
            
            var gameItems = FindGameItems(htmlContent);
            
            Console.WriteLine($"Найдено элементов игры: {gameItems.Count}");

            foreach (var item in gameItems)
            {
                var gameInfo = ExtractGameInfo(item);
                if (gameInfo != null)
                {
                    results.Add(gameInfo);
                }
            }

            return results;
        }

        private List<string> FindGameItems(string html)
        {
            var items = new List<string>();
            
            // Пробуем несколько паттернов для поиска игр
            var patterns = new[]
            {
                @"<li[^>]*class=""[^""]*psw-l-w-[^""]*""[^>]*>.*?</li>",
                @"<li[^>]*class=""[^""]*product-tile[^""]*""[^>]*>.*?</li>",
                @"<div[^>]*class=""[^""]*product-grid-cell[^""]*""[^>]*>.*?</div>",
                @"<div[^>]*class=""[^""]*grid-cell[^""]*""[^>]*>.*?</div>",
                @"<div[^>]*data-qa=""[^""]*gameTile[^""]*""[^>]*>.*?</div>"
            };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    items.Add(match.Value);
                }
                
                if (items.Count > 0)
                {
                    Console.WriteLine($"Найдено {items.Count} элементов по паттерну: {pattern}");
                    break;
                }
            }

            // Если ничего не нашли, пробуем найти по изображениям
            if (items.Count == 0)
            {
                var imgPattern = @"<img[^>]*src=""[^""]*image\.api\.playstation\.com[^""]*""[^>]*>";
                var imgMatches = Regex.Matches(html, imgPattern, RegexOptions.IgnoreCase);
                
                foreach (Match imgMatch in imgMatches)
                {
                    // Находим родительский li или div для каждого изображения
                    var parentPattern = @"<li[^>]*>.*?" + Regex.Escape(imgMatch.Value) + @".*?</li>";
                    var parentMatch = Regex.Match(html, parentPattern, RegexOptions.Singleline);
                    
                    if (parentMatch.Success)
                    {
                        items.Add(parentMatch.Value);
                    }
                }
            }

            return items;
        }

        private GameResult ExtractGameInfo(string itemHtml)
        {
            var name = ExtractGameName(itemHtml);
            if (string.IsNullOrEmpty(name))
                return null;

            var coverUrl = ExtractCoverUrl(itemHtml);
            if (string.IsNullOrEmpty(coverUrl))
                return null;

            var price = ExtractPrice(itemHtml);
            var gameType = ExtractGameType(itemHtml);
            
            // Фильтруем только игры и бандлы
            if (!IsGameOrBundle(gameType))
                return null;
            
            coverUrl = NormalizeCoverUrl(coverUrl);

            return new GameResult
            {
                Name = name,
                Price = price,
                Type = gameType,
                CoverUrl = coverUrl,
                Status = "Не скачано"
            };
        }

        // Метод для фильтрации по типу
        private bool IsGameOrBundle(string gameType)
        {
            if (string.IsNullOrEmpty(gameType))
                return false;
            
            // Приводим к нижнему регистру для сравнения
            var type = gameType.ToLowerInvariant();
            
            // Разрешенные типы
            return type.Contains("game") || 
                   type.Contains("bundle") || 
                   type.Contains("игра") ||  // Для русской локализации
                   type == "full game" ||
                   type == "ps4 game" ||
                   type == "ps5 game" ||
                   type == "ps4™ game" ||
                   type == "ps5™ game";
            
            // Все что содержит DLC, add-on, edition (кроме game), season pass и т.д. будет отфильтровано
        }

        private string ExtractGameName(string html)
        {
            var pattern = @"<span[^>]*data-qa=""[^""]*#product-name[^""]*""[^>]*>([^<]*)</span>";
            var match = Regex.Match(html, pattern);
            if (match.Success)
                return match.Groups[1].Value.Trim();

            pattern = @"<span[^>]*class=""[^""]*product-name[^""]*""[^>]*>([^<]*)</span>";
            match = Regex.Match(html, pattern);
            if (match.Success)
                return match.Groups[1].Value.Trim();

            return null;
        }

        private string ExtractCoverUrl(string html)
        {
            var pattern = @"<img[^>]*data-qa=""[^""]*#image#image[^""]*""[^>]*src=""([^""]*)""";
            var match = Regex.Match(html, pattern);
            if (match.Success)
                return match.Groups[1].Value;

            pattern = @"<img[^>]*src=""([^""]*image\.api\.playstation\.com[^""]*)""";
            match = Regex.Match(html, pattern);
            if (match.Success)
                return match.Groups[1].Value;

            return null;
        }

        private string ExtractPrice(string html)
        {
            var pattern = @"<span[^>]*data-qa=""[^""]*#display-price[^""]*""[^>]*>([^<]*)</span>";
            var match = Regex.Match(html, pattern);
            if (match.Success)
                return match.Groups[1].Value.Trim();

            pattern = @"<span[^>]*class=""[^""]*price[^""]*""[^>]*>([^<]*)</span>";
            match = Regex.Match(html, pattern);
            if (match.Success)
                return match.Groups[1].Value.Trim();

            return "N/A";
        }

        private string ExtractGameType(string html)
        {
            var pattern = @"<span[^>]*data-qa=""[^""]*#product-type[^""]*""[^>]*>([^<]*)</span>";
            var match = Regex.Match(html, pattern);
            if (match.Success)
                return match.Groups[1].Value.Trim();

            pattern = @"<span[^>]*class=""[^""]*product-type[^""]*""[^>]*>([^<]*)</span>";
            match = Regex.Match(html, pattern);
            if (match.Success)
                return match.Groups[1].Value.Trim();

            return "Game";
        }

        private string NormalizeCoverUrl(string url)
        {
            if (url.Contains("w="))
            {
                url = Regex.Replace(url, @"w=\d+", "w=440");
                
                if (url.Contains("thumb=true"))
                    url = url.Replace("thumb=true", "thumb=false");
                
                if (url.Contains("&thumb="))
                {
                    url = Regex.Replace(url, @"&thumb=[^&]*", "");
                    url += "&thumb=false";
                }
            }
            else
            {
                if (url.Contains("?"))
                    url += "&w=440&thumb=false";
                else
                    url += "?w=440&thumb=false";
            }

            return url;
        }
    }
}