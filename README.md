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
        <p>Title: <input id="basicTitle"/></p>
        <p>Message: <input id="basicMessage"/></p>
        <p><button onclick="submit();">Send</button></p>
    </body>
    <script>
        const _socket = new WebSocket("ws://localhost:8077");
        _socket.onopen = (e) => {console.log("Connected!");};
        function submit() {
            const title = document.querySelector('#basicTitle').value;
            const message = document.querySelector('#basicMessage').value;
	    _socket.send(JSON.stringify({title: title, message: message}));
        }
    </script>
</html>
```

## Payload Specification
These are the JSON payloads you send to the server via the active WebSockets connection.

**Note**: The values seen are the default values when nothing is provided. Keep in mind that `custom` needs to be set to `true` for the custom notification to be used.
### Standard Notification
The minimum to provide for this is `title` and `message`, those are mandatory to be able to show a basic SteamVR notification.
```jsonc
{
    "imageData": "", // Optional: A base64 string with *.png image data, only the data part of a data URL
    "basicTitle": "", // The title above the notification
    "basicMessage": "" // The main message of the notification
}
```
### Custom Notification
The way the transitions work, it will animate values that differs from when it is static, so setting `opacityPer` to `0` means it will transition from 0% to 100% when appearing, and from 100% to 0% when disappearing.
```jsonc
{
    "imageData": "", // A base64 string with *.png image data, only the data part of a data URL
    "customProperties": {
        "enabled": false, // True to do a custom notification
        "anchorType": 1, // What to anchor the notification to, 0: world, 1: head, 2: left hand, 3: right hand
        "attachToAnchor": false, // Will fix the overlay to the anchor, overrides attachToHorizon and alignToHorizon
        "attachToHorizon": false, // Will ignore pitch of the headset and keep the origin leveled with the horizon
        "alignToHorizon": false, // Initial roll alignment to the horizon, else relative to anchor
	"overlayChannel": 0, // Use different channels to show notifications simultaneously
        "animationHz": -1, // Animation frame rate, -1 uses the headset refresh rate instead
        "durationMs": 5000, // The time the notification stays up, in milliseconds
        "opacityPer": 1, // The opacity of the notificaiton when it is idle
        "widthM": 1, // The physical width of the overlay, in meters
        "zDistanceM": 1, // Distance, nearer (-) or further (+), in meters
        "yDistanceM": 0, // Vertical distance, down (-) or up (+), in meters
        "xDistanceM": 0, // Horizontal distance, left (-) or right (+), in meters
        "yawDeg": 0, // Yaw, left (-) or right (+), in degrees
        "pitchDeg": 0, // Pitch, down (-) or up (+), in degrees
        "rollDeg": 0, // Roll, right (-) or left (+), in degrees
        "follow": {
            "enabled": false, // Set this to true to enable the follow mode, repositions overlay when facing away
            "triggerAngle": 65, // Threshold angle to trigger respoitioning, in degrees
            "durationMs": 250, // Duration of the transition to new location, in milliseconds
            "tweenType": 5 // Tween mode, see next section for a full list
        },
        "animations": [
            {
                "property": 0, // 0: None (disabled), 1: Yaw, 2: Pitch, 3: Roll, 4: Pos Z, 5: Pos Y, 6: Pos X, 7: Scale, 8: Opacity
                "amplitude": 1, // Strength of the effect, degrees for angles, meters for positions, percent for scale & opacity
                "frequency": 1, // Times per second for the loop to happen, or speed for a linear animation
                "phase": 0, // 0: Sine, 1: Cosine, 2: Negative Sine, 3: Negative Cosine
                "waveform": 0, // 0: PhaseBased (more to be added)
                "flipWaveform": false // Will flip the waveform upside down
            }
            /*
             * You can add as many different animations as you want here, with one limitation:
             * A property can only have one animation applied, this as multiples will override each other.
             */
        ],
        "transitions": [
            {
                "scalePer": 1, // Normalized scale, 1 = 100%
                "opacityPer": 0, // Normalized opacity where 1 = 100%
                "zDistanceM": 0, // Horizontal translation, in meters
                "yDistanceM": 0, // Vertical translation, in meters
                "xDistanceM": 0, // Distance from headset, in meters
                "rollDeg": 0, // Roll rotation, left(-) or right (+) in degrees
                "durationMs": 250, // Length of animation, in milliseconds
                "tweenType": 5 // Tween mode, see next section for a full list
            }
            /* 
            * You can add a second object here to change the outgoing transition.
            * This is optional and will be used if provided, otherwise the out
            * transition defaults to the same as in but reversed.
            */
        ],
        "textAreas": [
            {
                "text": "", // The text to render on the image
                "xPositionPx": 0, // Horizontal position of the text area on the image, in pixels
                "yPositionPx": 0, // Vertical position of the text area on the image, in pixels
                "widthPx": 100, // Width of the text area, in pixels
                "heightPx": 100, // Height of the text area, in pixels
                "fontSizePt": 10, // Font size of the text, in points
                "fontFamily": "", // The name of the font to be used
                "fontColor": "", // The text color, HTML based, so hex code or name
                "horizontalAlignment": 0, // Horizontal alignment, 0 = left, 1 = center, 2 = right
                "verticalAlignment": 0, // Vertical alignment, 0 = left, 1 = center, 2 = right
            }
            /*
            * You can add more text areas by filling up this array with more objects.
            */
        ]
    }
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
