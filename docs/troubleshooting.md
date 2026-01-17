View all logs for the overseer service

`journalctl -u overseer`

View logs and follow in real-time (like tail -f)

`journalctl -u overseer -f`

View only the most recent logs (last 100 lines)

`journalctl -u overseer -n 100`

View logs since last boot

`journalctl -u overseer -b`

View logs from today only

`journalctl -u overseer --since today`

View current service status and recent log entries

`systemctl status overseer`
