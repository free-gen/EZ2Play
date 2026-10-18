
namespace EZ2Play.App
{
    /// <summary>
    /// Represents a game returned by a SteamGridDB search.
    /// </summary>
    /// <remarks>
    /// Stores the SteamGridDB game identifier and name.
    ///
    /// DisplayId provides the secondary identifier text
    /// shown separately from the game name.
    /// </remarks>
    public class ParserGameResult
    {
        /// <summary>
        /// Gets or sets the SteamGridDB game identifier.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the game name returned by SteamGridDB.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the secondary identifier text
        /// displayed next to the game name.
        /// </summary>
        public string DisplayId => $"GAME ID: {Id}";
    }
}
