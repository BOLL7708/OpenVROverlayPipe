# OpenVRNotificationPipe
WebSocket server that lets you submit a payload that results in a SteamVR or custom notification, download the latest release [here](https://github.com/BOLL7708/OpenVRNotificationPipe/releases).

## What does it do?
Through WebSockets you can display notifications inside SteamVR, either using the built in SteamVR notifications or custom image-based notifications with many options for transitions and placement.

[![Short video demonstration](https://img.youtube.com/vi/gSqyOYsiymw/0.jpg)](https://www.youtube.com/watch?v=gSqyOYsiymw)

## How do I use it?
Run SteamVR, then run this application, if both the OpenVR and Server status are green you are good to go. To see an example of what it does, and how to do those things, click the `example` link in the interface to open a self contained webpage that connects to this application and lets you try it out.

## Compatible applications
If you made something that is publicly available that use this pipe application, it can be listed here.
* [Streaming Widget](https://github.com/BOLL7708/streaming_widget): Streaming browser source made specifically for OBS, Twitch, SteamVR and a range of other solutions.
* [Twitch Logger](https://github.com/jeppevinkel/twitch-logger): Logs Twitch chat to disk and can pipe them to Discord and VR.

## Minimal WebSockets Client
To send things to this application you need a WebSockets client, you can easily do this directly in a browser by opening a static page, below is example code you can use to quickly send a standard notification. If you have set a different server port you need to update that in the server URI in this example.
```html
<html>
    <body>
        <p>Title: <input id="title"/></p>
        <p>Message: <input id="message"/></p>
        <p><button onclick="submit();">Send</button></p>
    </body>
    <script>
        const _socket = new WebSocket("ws://localhost:8077");
        _socket.onopen = (e) => {console.log("Connected!");};
        function submit() {
            const title = document.querySelector('#title').value;
            const message = document.querySelector('#message').value;
	    _socket.send(JSON.stringify({title: title, message: message}));
        }
    </script>
</html>
```

## Payload Specification
These are the JSON payloads you send to the server via the active WebSockets connection.

**Note**: The values seen are the default values when nothing is provided. Keep in mind that `custom` needs to be set to `true` for the custom notification to be used.
### Standard Notification
The minimum to provide for this is `title` and `message`, those are mandatory to be able to show a notification at all.
```jsonc
{
    "custom":false, // True to do a custom notification
    "title":"", // The title above the notification
    "message":"", // The main message of the notification
    "image": "" // The base64 string of a .png image
}
```
### Custom Notification
The way the transitions work, it will animate values that differs from when it is static, so setting `opacity` to `0` means it will transition from 0% to 100% when appearing, and from 100% to 0% when disappearing.
```jsonc
{
    "custom": false, // Needs to be set to true, false is the default though
    "image": "", // A base64 string with *.png image data, only the data if data URL
    "properties": {
        "headset": false, // Stay fixed to the headset
        "horizontal": true, // Initial alignment to the horizon, else headset
        "level": false, // Will ignore pitch of the headset and keep origin level
	"channel": 0, // Use different channels to show notifications simultaneously
        "hz": -1, // Animation frame rate, -1 uses the headset refresh rate instead
        "duration": 1000, // The time the notification stays up, in milliseconds
        "width": 1, // The physical width of the overlay, in meters
        "distance": 1, // Distance from the headset to the notification, in meters
        "pitch": 0, // Vertical rotation, down (-) or up (+), in degrees
        "yaw": 0, // Horizontal rotation, left (-) or right (+), in degrees
	"offsetx": 0, // Horizontal image offset, left (-) or right (+), in meters
	"offsety": 0 // Vertical image offset, down (-) or up (+), in meters
    },
    "transitiona": [
	{
	    "scale": 1, // Normalized scale, 1 = 100%
	    "opacity": 0, // Normalized opacity where 1 = 100%
	    "horizontal": 0, // Horizontal translation, in meters
	    "vertical": 0, // Vertical translation, in meters
	    "distance": 0, // Distance from headset, in meters
	    "spin": 0, // Roll rotation, left(-) or right (+) in degrees
	    "tween": 0, // Tween mode, see next section for a full list
	    "duration": 100 // Length of animation, in milliseconds
	}
        /* 
	 * You can add a second object here to change the outgoing transition.
         * This is optional and will be used if provided, otherwise the out
	 * transition defaults to the same as in but reversed.
         */
    ],
    "textAreas": {
	"posx": 0, // Horizontal position of the text area on the image, in pixels
	"posy": 0, // Vertical position of the text area on the image, in pixels
	"width": 100, // Width of the text area, in pixels
	"height": 100, // Height of the text area, in pixels
	"size": 10, // Font size of the text
	"text": "", // The text to render on the image
	"font": "", // The name of the font to be used
	"color": "", // The text color, HTML based, so hex code or name
	"gravity": 0, // Alignment in the text area, 0 = left, 1 = center, 2 = right
	"alignment": 0, // Alignment on the line, 0 = left, 1 = center, 2 = right
    }
    /*
     * You can add more text areas by filling up this array with more objects.
     */
}
```
#### Tween modes
Most of these modes have been acquired from [Easings.net](https://easings.net/), check that page out for what they do or just try them out.

0. Linear (Default unmodified, hard stop)
1. Sine (Based on the sine curve, soft stop)
2. Quadratic (^2, soft stop)
3. Cubic (^3, soft stop)
4. Quartic (^4, soft stop)
5. Quintic (^5, soft stop)
6. Circle (Based on a circle, soft stop)
7. Back (Overshoots but comes back, soft stop)
8. Elastic (Jiggling, soft stop)
9. Bounce (Multiple bounces, hard stop)
