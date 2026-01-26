# Overseer

Overseer is a small utility that allows for monitoring multiple 3D Printers from a single user interface.

Includes support for:

- [Octoprint](https://github.com/foosel/OctoPrint)
- [RepRapFirmware](https://github.com/dc42/RepRapFirmware)
- [Elegoo](https://wiki.elegoo.com/en/Centauri-carbon)
- [Bambu Labs\*](https://wiki.bambulab.com/en/x1)

\* Only tested with older versions in LAN-only mode with the X1 Carbon. It may work in dev mode with the latest firmware.

**WARNING:** It is recommended that you do **NOT** configure Overseer to be accessible outside of your local network. Do so at your own risk.

![Screenshot](/preview.jpg)

## Usage

There are two ways to use the Overseer application. Which way works best for you depends on your specific use case and server configuration experience.

### 1. Web Application

The standalone application stores all configuration data locally in the browser, so each browser/device needs to be configured individually.

**CAUTION:** While the standalone web application supports readonly accounts, and these accounts are restricted from performing certain operations through the user interface, the data is stored in the browser and accessible through browser development tools. This means a savvy user could access the printer information and invoke the APIs directly.

**CAUTION:** The standalone web application can connect to instances using self-signed certificates and certificate authentication. However, it requires that the certificate is accepted/configured within the browser.

**CAUTION:** The standalone web application requires CORS to be enabled.

[Enable CORS for Octoprint](https://docs.octoprint.org/en/master/api/general.html#cross-origin-requests)
[Enable CORS for RepRapFirmware](https://docs.duet3d.com/User_manual/Reference/Gcodes#m586-configure-network-protocols)

**CAUTION:** If you do not understand any of the caution messages, please don't use the web app version. This applies to overseer.live and the self-hosted site.

#### Overseer.live

The Standalone Web Application is published online and available at [https://overseer.live](https://overseer.live). This works completely in the browser and no data is sent to any third-party servers.

#### Self Hosting

You can also host the application on your local network. Since the application consists of only static files, the configuration is very simple and can be hosted on an Octopi instance along with Octoprint with minimal overhead.

Please refer to the [wiki page](https://github.com/overseerapp/overseer/wiki/Overseer-Standalone-Web-App) for configuration instructions.

### 2. Server

This is a cross-platform server-based solution that allows you to host Overseer on your local network. All configuration is stored in a centralized database allowing the application to be configured once and accessible from multiple browsers/devices.

Please refer to the [wiki page](https://github.com/overseerapp/overseer/wiki/Overseer-Daemon-%28.Net-Core%29) for configuration instructions.
