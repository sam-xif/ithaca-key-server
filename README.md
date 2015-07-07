# ithaca-key-server

Key authentication server for the Ithaca Minecraft server website.

Due to the same origin policy, it is advised that the web server acts as a middleman for connections to this server.
This can be accomplished through assigning the website user a session ID, or extending the timeout of the javascript query. Depending on how many keys the server has to search through (which could potentially be thousands), the response may take a few seconds.
