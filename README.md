# yupaste

A tiny shared-clipboard pastebin for moving text between devices on the same network.
One text box, shared by everyone connected: hit **Upload** to push your edits to all
other viewers, or **Copy to clipboard** to grab the current contents.

## Build

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
./build.sh                    # builds for linux-x64 by default
./build.sh linux-arm64        # or target another runtime
./build.sh --install          # also install to ~/.local/bin as `yupaste`
./build.sh linux-arm64 --install
```

This produces a single self-contained binary at `publish/yupaste` — no .NET
runtime needs to be installed on the machine that runs it. With `--install`,
it's also copied to `~/.local/bin/yupaste`; if that directory isn't on your
PATH, the script prints the line to add to your shell rc file.

## Run

```bash
publish/yupaste --port 5005
```

Then, from any device on the same network, open `http://<host-ip>:5005/` in a
browser.

Options:

- `--port <n>` — port to listen on (default `5005`)
- `--host <addr>` — address to bind to (default `0.0.0.0`, all interfaces)

The port can also be set via the `YUPASTE_PORT` environment variable.

## Notes

- State is in-memory only — restarting the process clears the shared text.
- There's no auth; anyone who can reach the port can read/write the paste.
  Only run it on networks you trust (e.g. your LAN), or put it behind a
  reverse proxy / firewall if exposing it further.
