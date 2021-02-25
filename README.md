# OpenVRNotificationPipe
WebSocket server that lets you submit a payload that results in a SteamVR or custom notification, download the latest release [here](https://github.com/BOLL7708/OpenVRNotificationPipe/releases).

## What does it do?
Through WebSockets you can display notifications inside SteamVR. Either with the build in SteamVR notifications or custom notifications in the form of submitted images including many options for transitions.

[![Short video demonstration](https://img.youtube.com/vi/gSqyOYsiymw/0.jpg)](https://www.youtube.com/watch?v=gSqyOYsiymw)

## How do I use it?
Run SteamVR, then run this application, if both the OpenVR and Server status are green you are good to go. To see an example of what it does, and how to do those things, click the `example` link to open a self contained webpage that connects to this application and lets you try it out.

## Compatible applications
If you made something that is publicly available that use this pipe application, it can be listed here.
* [Twitch Logger](https://github.com/jeppevinkel/twitch-logger): Logs Twitch chat to disk and can pipe them to Discord and VR.

## Minimal WebSockets Client
To send things to this notification you need a WebSockets client, you can easily do this directly in a browser, below is example code you can use to quickly send a standard notification. If you have set a different code you need to update that in the server URI in this example.
```html
<html>
    <body>
        <p>Title: <input id="title"/></p>
        <p>Message: <input id="message"/></p>
        <p><button onclick="submit();">Send</button></p>
    </body>
    <script>
        var _socket = new WebSocket("ws://localhost:8077");
        _socket.onopen = (e) => {console.log("Connected!");};
        function submit() {
            let title = document.querySelector('#title').value;
            let message = document.querySelector('#message').value;
	    _socket.send(JSON.stringify({title: title, message:message}));
        }
    </script>
</html>
```

## Payload Specification
These are the JSON payloads you send to the server via the active WebSockets connection.

**Note**: The values seen are the default values when not provided. Keep in mind that `custom` needs to be set to `true` for the custom notification to be used.
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
    "custom":false, // Needs to be set to true, false is the default though
    "image": "", // A base64 string with *.png image data, only the data if data URL
    "properties":{
        "headset":false, // Stay fixed to the headset
        "horizontal":true, // Initial alignment to the horizon, else headset
        "hz":-1, // Animation frame rate, -1 uses the headset refresh rate instead
        "duration": 1000, // The time the notification stays up, in milliseconds
        "width": 1, // The physical width of the overlay, in meters
        "distance": 1, // Distance from the headset to the notification, in meters
        "pitch": 0, // Vertical rotation, down (-) or up (+), in degrees
        "yaw": 0 // Horizontal rotation, left (-) or right (+), in degrees
    },
    "transition":{
        "scale": 1, // Normalized scale, 1 = 100%
        "opacity": 0, // Normalized opacity where 1 = 100%
        "vertical": 0, // Vertical translation, in meters
        "distance": 0, // Distance from headset, in meters
        "horizontal": 0, // Horizontal translation, in meters
        "spin": 0, // Roll rotation, left(-) or right (+) in degrees
        "tween": 0, // Tween mode, see below
        "duration": 100 // Length of animation, in milliseconds
    },
    "transition2":{
        /* 
	     * This is optional and will be used if provided.
         * It should contain the same fields as "transition"
         * but will be used for the transition out which
         * otherwise defaults to the same as in but reversed.
         */
    }
}
```
#### Tween modes
Most of these modes have been acquired from [Easings.net](https://easings.net/), check that page out for what they do or just try them out.

0. Linear (default)
1. Sine
2. Quadratic
3. Cubic
4. Quartic
5. Quintic
6. Circle
7. Back
8. Elastic
9. Bounc