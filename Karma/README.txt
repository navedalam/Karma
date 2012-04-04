In typical filesharing scenarios, if a file is not available on the network it will not show in search results. In Karma, all files will show in search results whether they are offline or online. The user generally will select online files, however if a file is not available the user may select an offline file to download.
Whenever the file is next online, and the user is also online, the server will start the file transfer automatically. However, if the file is online, but the requesting user is not online, the file will be forwarded to the user’s friends. Here, Karma uses the store and forward technique with certain modifications – it forwards data to friends of (or people likely to be online with) the user who has requested a file rather than a node closer in the route.

Features

1.  Karma Client –This is the software that will be deployed on the peers of the network. It is written in C#
a.  Intuitive and usable UI, installer
b.  Support for download, upload and search
c.  The ability to cancel downloads
d.  Can follow remotely given instructions by the server to store and forward data
e.  Can send files to requesting clients
f.  Complete configurability – setting servers/backup servers, download folders, settings, shared folders etc.
g.  Indexer and hasher for local files
h.  Completely multithreaded – minimum of 4 threads running simultaneously

2.  Karma Server –Deployed on the server, written in Python.
a.  Completely multithreaded - maintains multiple TCP connetions with peers
b.  Syncing and managing list of files available on the network
c.  Managing active users, along with nicknames
d.  Managing and calculating friendships, and using these to increase file availability using the store-and-forward mechanism.
e.  Responding to peer search queries
f.  Tracking pending file transfer requests