using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.API;
using Discord.Net;
using Discord.WebSocket;
using Sandbox;
using Sandbox.ModAPI;

namespace DiscordPlugin
{
    public class DiscordManager
    {
        private static DiscordManager _instance;
        private DiscordSocketClient _client;
        //private Server _kshServer;
        public ReadOnlyCollection<SocketGuild> Servers { get; private set; }

        public ReadOnlyCollection<SocketChannel> Channels { get; private set; }

        public enum NotificationModeEnum
        {
            Mention,
            PM,
            All,
            None
        }

        public NotificationModeEnum NotificationMode;

        public static DiscordManager Instance
        {
            get
            {
                if ( _instance == null )
                    _instance = new DiscordManager();
                return _instance;
            }
        }

        public enum LoginResult
        {
            None,
            Ok,
            Invalid,
            RateLimit,
            Error,
        }

        public async Task<LoginResult> Login( string token)
        {
            if ( _client == null )
                _client = new DiscordSocketClient();
            try
            {
                await _client.LoginAsync(TokenType.User, token);
            }
            catch (HttpException ex)
            {
                MySandboxGame.Log.WriteLineAndConsole("##Discord Plugin: Invalid login details!" + ex);
                _client = null;
                return LoginResult.Invalid;
            }
            catch (RateLimitedException ex)
            {
                MySandboxGame.Log.WriteLine("##Discord Plugin: User is rate limited!" + ex);
                _client = null;
                return LoginResult.RateLimit;
            }
            catch (Exception ex)
            {
                MySandboxGame.Log.WriteLine("##Discord Plugin: Error during login!" + ex);
                _client = null;
                return LoginResult.Error;
            }
            //MAGIC DO NOT TOUCH
            //this us the ID of the KSH server
            //_kshServer = _client.GetServer( 125011928711036928 );

            //TODO: server is null if player hasn't joined. Do something?
            //if ( _kshServer == null )
            //    return false;
            await _client.SetGame("dat engineering game");
            return LoginResult.Ok;
        }

        public void RegisterRecieve()
        {
            _client.MessageReceived += MessageReceived;
        }

        private Task MessageReceived(SocketMessage message)
        {
            MyAPIGateway.Utilities.ShowMessage(message.Author.Username, message.Content);
            return Task.CompletedTask;
        }

        public string ServerList()
        {
            if ( _client == null )
                return null;

            var result = new StringBuilder();
            int count = 0;
            foreach ( var server in _client.Guilds )
                result.Append( $"[{count++}]: {server.Name}\r\n" );
            return result.ToString();
        }

        public string ChannelList()
        {
            if ( _client == null )
                return null;

            var result = new StringBuilder();
            int count = 0;
            foreach ( var server in Servers )
            {
                result.Append( $"{server.Name}:" );
                result.AppendLine();
                foreach ( var channel in server.Channels )
                {
                    while (!channel.Users.Any()) { }
                    if (!channel.Users.Any(x => x.Id == _client.CurrentUser.Id))
                        continue;
                    
                    result.Append( $"\t[{count++}]: {channel.Name}" );
                    result.AppendLine();
                }
                result.AppendLine();
            }
            return result.ToString();
        }

        public void PopulateServers( int[] serverIndicies )
        {
            int counter = 0;
            var servList = new List<SocketGuild>();
            foreach ( var server in _client.Guilds )
            {
                if ( serverIndicies.Contains( counter ) )
                {
                    servList.Add( server );
                }
                counter++;
            }
            Servers = new ReadOnlyCollection<SocketGuild>( servList );
        }

        public void PopulateChannels( int[] channelIndicies )
        {
            int counter = 0;
            var chanList = new List<SocketChannel>();
            foreach ( var server in Servers )
            {
                foreach ( var channel in server.Channels )
                {
                    while (!channel.Users.Any()) { }
                    if ( channel.Users.Any( x => x.Id == _client.CurrentUser.Id ) )
                    {
                        if ( channelIndicies.Contains( counter ) )
                            chanList.Add( channel );
                        counter++;
                    }
                }
            }
            Channels = new ReadOnlyCollection<SocketChannel>( chanList );
        }

    }
}
