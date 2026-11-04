# yupaste

A tiny shared-clipboard pastebin for moving text and files between devices on the
same network. One text box, shared by everyone connected: hit **Upload** to push your
edits to all other viewers, or **Copy to clipboard** to grab the current contents.

It can also host files: drop them on the page and anyone connected can download
them. One file downloads as-is; several download together as a zip. Files share a
64 MB limit, shown by the gauge on the page, and **Allow 128 MB** raises it. To go
back to 64 MB, first remove files until they fit.

yupaste shuts itself down after 15 minutes without activity, so a forgotten instance
doesn't stay exposed on the network.

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
publish/yupaste --port 5005   # or just `yupaste` if you built with --install
```

Then, from any device on the same network, open `http://<host-ip>:5005/` in a
browser. To connect from a phone, scan the QR code printed in the terminal, or
the one behind the **QR** button on the page.

Options:

- `--port <n>` — port to listen on (default `5005`)
- `--host <addr>` — address to bind to (default `0.0.0.0`, all interfaces)
- `--idle-timeout <minutes>` — shut down after this long without activity
  (default `15`, `0` to never shut down)

The port can also be set via the `YUPASTE_PORT` environment variable.

## Notes

- For the idle shutdown, page loads, uploads, downloads and other changes count as
  activity, and the timer doesn't run during a transfer. A tab that's merely open
  doesn't count, and neither does typing before you hit **Upload**. The page shows
  a countdown.
- State is in-memory only — shutting down or restarting clears the shared text and
  files.
- There's no auth; anyone who can reach the port can read/write the paste, and
  upload, download or remove files.
  Only run it on networks you trust (e.g. your LAN), or put it behind a
  reverse proxy / firewall if exposing it further.
