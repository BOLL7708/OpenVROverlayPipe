# OpenVRNotificationPipe
WebSocket server that lets you submit a payload that results in a SteamVR or custom notification, download the latest release [here](https://github.com/BOLL7708/OpenVRNotificationPipe/releases).

## What does it do?
Through WebSockets you can display notifications inside SteamVR. Either with the build in SteamVR notifications or custom notifications in the form of submitted images including many options for transitions.

[![Short video demonstration](https://img.youtube.com/vi/gSqyOYsiymw/0.jpg)](https://www.youtube.com/watch?v=gSqyOYsiymw)

## How do I use it?
Run SteamVR, then run this application, if both the OpenVR and Server status are green you are good to go. To see an example of what it does, and how to do those things, click the `example` link to open a self contained webpage that connects to this application and lets you try it out.

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
```js
{
    "custom":false, // True to do a custom notification
    "title":"", // The title above the notification
    "message":"", // The main message of the notification
    "image": "" // The base64 string of a .png image
}
```
### Custom Notification
The way the transitions work, it will animate values that differs from when it is static, so setting `opacity` to `0` means it will transition from 0% to 100% when appearing, and from 100% to 0% when disappearing.
```js
{
    "custom":false, // Needs to be set to true
    "image": "", // The base64 string of a .png image
    "properties":{
        "headset":false, // Stay aligned to the headset
        "horizontal":true, // Align to the horizon
        "hz":-1, // Animation Hz, -1 means use headset Hz
        "duration": 1000, // Time to stay up in ms
        "width": 1, // Width of overlay in meters
        "distance": 1, // Distance from headset in meters
        "pitch": 0, // Rotate down (-) or up (+) degrees
        "yaw": 0, // Rotate left (-) or right (+) degrees
    },
    "transition":{
        "scale": 1, // Scale where 1 = 100%
        "opacity": 0, // Opacity where 1 = 100%
        "vertical": 0, // Vertical translation in meters
        "distance": 0, // Distance from headset in meters
        "horizontal": 0, // Horizontal translation in meters
        "spin": 0, // Rotation in degrees
        "interpolation": 0, // Interpolation, see below
        "duration": 100 // Length of transition in ms
    },
    "transition2":{
        // This is optional and will be used if provided.
        // It should contain the same fields as "transition"
        // but will be used for the transition out which
        // otherwise defaults to the same as in.
    }
}
```
