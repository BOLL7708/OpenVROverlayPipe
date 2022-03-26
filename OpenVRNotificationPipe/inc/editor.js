// Websocket 
let _ws = null
let _wsActive = false
const _localStorageKey = 'OpenVRNotificationPipe.BOLL7708.Config'

// Construct forms
const _formSubmit = document.querySelector('#formSubmit')
const _formImage = document.querySelector('#formImage')
const _formProperties = document.querySelector('#formProperties')
const _formFollow = document.querySelector('#formFollow')
const _formAnimation1 = document.querySelector('#formAnimation1')
const _formAnimation2 = document.querySelector('#formAnimation2')
const _formAnimation3 = document.querySelector('#formAnimation3')
const _templateAnimation = document.querySelector('#templateAnimation')
const _formTransitionIn = document.querySelector('#formTransitionIn')
const _formTransitionOut = document.querySelector('#formTransitionOut')
const _formTextarea = document.querySelector('#formTextarea')
const _templateTransition = document.querySelector('#templateTransition')
const _templateTween = document.querySelector('#templateTween')
const _formConfig = document.querySelector('#formConfig')
_formFollow.appendChild(_templateTween.content.cloneNode(true))
_formAnimation1.appendChild(_templateAnimation.content.cloneNode(true))
_formAnimation2.appendChild(_templateAnimation.content.cloneNode(true))
_formAnimation3.appendChild(_templateAnimation.content.cloneNode(true))
_formTransitionIn.appendChild(_templateTransition.content.cloneNode(true))
_formTransitionIn.appendChild(_templateTween.content.cloneNode(true))
_formTransitionOut.appendChild(_templateTransition.content.cloneNode(true))
_formTransitionOut.appendChild(_templateTween.content.cloneNode(true))

// Input wrapping & IDing
const _elements = document.querySelectorAll('input, select, canvas')
_elements.forEach(el => {
    // ID
    const form = el.parentElement
    const name = el.name ?? el.id
    const id = `${form.id}-${name}`
    el.id = id
    
    // Wrapping + label
    const wrapper = document.createElement('p')
    el.parentNode.insertBefore(wrapper, el)
    const label = document.createElement('label')
    label.setAttribute('for', id)
    label.innerHTML = name.toLowerCase() == 'tweentype' 
        ? `<a href="https://easings.net/" target="_blank">${name}</a>`
        : `${name}:`
    wrapper.appendChild(label)
    wrapper.appendChild(el)
})

// General elements
const _submit = document.querySelector('#submit')
_submit.addEventListener('click', sendNotification)

// Canvas slements
let _imgData = null;
const _canvas = document.querySelector('#formImage-canvas')
const _ctx = _canvas.getContext('2d')
const _file = document.querySelector('#formImage-file')
_file.addEventListener('change', readImage)

// Config elements
const _config = document.querySelector('#config')

const _copyJSON = document.querySelector('#copyJSON')
_copyJSON.addEventListener('click', copyConfigJSON)
const _downloadJSON = document.querySelector('#downloadJSON')
_downloadJSON.addEventListener('click', downloadConfigJSON)

const _copyJS = document.querySelector('#copyJS')
_copyJS.addEventListener('click', copyConfigJS)
const _downloadJS = document.querySelector('#downloadJS')
_downloadJS.addEventListener('click', downloadConfigJS)

// Key listener
document.onkeyup = function (e) {
    e = e || window.event
    switch(e.code) {
        case 'NumpadEnter':
        case 'Enter': 
            sendNotification(e)
            break
        default: 
            // console.log(e.code)
            break
    }
}

// Program loop
function init()
{
    connectLoop()
    /*
    // TODO: Make this an option.
    _config.innerHTML = localStorage.getItem(_localStorageKey) ?? ''
    if(_config.innerHTML.length > 0) {
        loadConfig()
    }
    */
}
function connectLoop() 
{
    if(!_wsActive) {
        const url = new URL(window.location.href)
        const params = new URLSearchParams(url.search)
        const port = params.get('port') ?? 8077

        // TODO: Save port in local data

        var wsUri = `ws://localhost:${port}`
        _wsActive = true
        if(_ws != null) _ws.close()
        try {
            _ws = new WebSocket(wsUri)
            _ws.onopen = function(evt) { 
                _wsActive = true
                document.title = 'Pipe Editor - CONNECTED'
                _submit.disabled = false
            }
            _ws.onclose = function(evt) { 
                _wsActive = false
                document.title = 'Pipe Editor - DISCONNECTED'
                _submit.disabled = true
             }
            _ws.onmessage = function(evt) {
                const data = JSON.parse(evt.data)
                console.log(data)
             }
            _ws.onerror = function(evt) { 
                _wsActive = false
                console.error(`WebSocket Connection Error`)
                document.title = `Pipe Editor - ERROR`
            }
        } catch (error) {
            console.error(`WebSocket Init Error: ${error}`)
            _wsActive = false
            document.title = `Pipe Editor - ERROR`
        }
    }
    setTimeout(connectLoop, 5000);
}

// Image functions
function readImage() {
    _formImage.classList.remove('unset')
    
    if(this.files && this.files[0]) {
        var FR = new FileReader()
        FR.onload = function(e) {
               var img = new Image()
               img.addEventListener("load", function() {
                   _canvas.width = img.width
                _canvas.height = img.height
                _ctx.clearRect(0, 0, _canvas.width, _canvas.height)
                _ctx.drawImage(img, 0, 0, img.width, img.height)
               });
               img.src = e.target.result
               _imgData = e.target.result.split(',')[1]
        };       
        FR.readAsDataURL( this.files[0] )
    }
}
function getImageData() {
    return _imgData
}
function getImagePath() {
    return 'C:/replace/with/path/on/disk/'+_file.files[0].name
}

// Data
function sendNotification(e) {
    e?.preventDefault()
    const data = getData()
    // Send data
    _ws.send(JSON.stringify(data))
    
    // TODO: Change be a config load/save management system
    // window.localStorage.setItem(_localStorageKey, JSON.stringify(data))
}

function getData() {
    var data = {
        // General
        imageData: getImageData(),
        imagePath: getImagePath(),
        
        // Standard
        basicTitle: "",
        basicMessage: "",

        // Custom
        customProperties: {
            follow: {},
            animations: [],
            transitions: [],
            textAreas: []
        }
    };

    // Get all form data
    const propertiesData = new FormData(_formProperties)
    const propertiesJSON = Object.fromEntries(propertiesData.entries())
    propertiesJSON.anchorType = propertiesData.getAll('anchorType').pop()

    const followData = new FormData(_formFollow)
    const followJSON = Object.fromEntries(followData.entries())
    followJSON.tweenType = followData.getAll('tweenType').pop()

    const animationData1 = new FormData(_formAnimation1)
    const animationJSON1 = Object.fromEntries(animationData1.entries())
    animationJSON1.property = animationData1.getAll('property').pop()
    animationJSON1.phase = animationData1.getAll('phase').pop()
    animationJSON1.waveform = animationData1.getAll('waveform').pop()

    const animationData2 = new FormData(_formAnimation2)
    const animationJSON2 = Object.fromEntries(animationData2.entries())
    animationJSON2.property = animationData2.getAll('property').pop()
    animationJSON2.phase = animationData2.getAll('phase').pop()
    animationJSON2.waveform = animationData2.getAll('waveform').pop()

    const animationData3 = new FormData(_formAnimation3)
    const animationJSON3 = Object.fromEntries(animationData3.entries())
    animationJSON3.property = animationData3.getAll('property').pop()
    animationJSON3.phase = animationData3.getAll('phase').pop()
    animationJSON3.waveform = animationData3.getAll('waveform').pop()

    const transitionsInData = new FormData(_formTransitionIn)
    const transitionsInJSON = Object.fromEntries(transitionsInData.entries())
    transitionsInJSON.tweenType = transitionsInData.getAll('tweenType').pop()

    const transitionsOutData = new FormData(_formTransitionOut)
    const transitionsOutJSON = Object.fromEntries(transitionsOutData.entries())
    transitionsOutJSON.tweenType = transitionsOutData.getAll('tweenType').pop()

    const textareaData = new FormData(_formTextarea)
    const textareaJSON = Object.fromEntries(textareaData.entries())
    textareaJSON.horizontalAlignment = textareaData.getAll('horizontalAlignment').pop()
    textareaJSON.verticalAlignment = textareaData.getAll('verticalAlignment').pop()

    // Fill data
    data.customProperties = propertiesJSON
    data.customProperties.follow = {}
    if(followJSON.enabled) data.customProperties.follow = followJSON
    data.customProperties.animations = []
    if(animationJSON1.property != 0) data.customProperties.animations.push(animationJSON1)
    if(animationJSON2.property != 0) data.customProperties.animations.push(animationJSON2)
    if(animationJSON3.property != 0) data.customProperties.animations.push(animationJSON3)
    data.customProperties.transitions = []
    data.customProperties.transitions.push(transitionsInJSON)
    data.customProperties.transitions.push(transitionsOutJSON)
    data.customProperties.textAreas = []
    if(textareaJSON.text.length > 0) data.customProperties.textAreas.push(textareaJSON)
    return data
}

function copyConfigJSON(e) {
    e?.preventDefault()
    const indent = _formSubmit.querySelector('#formSubmit-indentation').value
    const data = getData()
    data.imageData = ''
    _config.innerHTML = JSON.stringify(data, null, parseInt(indent))
    _config.select()
    document.execCommand('copy')
}

function downloadConfigJSON(e) {
    e?.preventDefault()
    const indent = _formSubmit.querySelector('#formSubmit-indentation').value
    const data = getData()
    data.imageData = ''
    const json = JSON.stringify(data, null, parseInt(indent))
    download(json, 'pipe-config.json', 'text/plain')
}

function copyConfigJS(e) {
    e?.preventDefault()
    const data = getData()
    data.imageData = ''
    _config.innerHTML = renderJS(data, null, 0)
    _config.select()
    document.execCommand('copy')
}

function downloadConfigJS(e) {
    e?.preventDefault()
    const data = getData()
    data.imageData = ''
    const json = renderJS(data, null, 0)
    download(json, 'pipe-config.js', 'text/plain')
}

// Function to download data to a file https://stackoverflow.com/a/30832210
function download(data, filename, type) {
    var file = new Blob([data], {type: type});
    if (window.navigator.msSaveOrOpenBlob) // IE10+
        window.navigator.msSaveOrOpenBlob(file, filename);
    else { // Others
        var a = document.createElement("a"),
                url = URL.createObjectURL(file);
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        setTimeout(function() {
            document.body.removeChild(a);
            window.URL.revokeObjectURL(url);  
        }, 0); 
    }
}

function loadConfig(e) {
    e?.preventDefault()
    const data = JSON.parse(_config.value)
    if(_config.value.length > 0) window.localStorage.setItem(_localStorageKey, _config.value)
    const properties = data.customProperties ?? {}
    const follow = data.customProperties.follow ?? {}
    const animation1 = data.customProperties.animations[0] ?? {}
    const animation2 = data.customProperties.animations[1] ?? {}
    const animation3 = data.customProperties.animations[2] ?? {}
    const transitionIn = data.customProperties.transitions[0] ?? {}
    const transitionOut = data.customProperties.transitions[1] ?? {}
    const textarea = data.customProperties.textAreas[0] ?? {}

    applyDataToForm(_formProperties, properties)
    applyDataToForm(_formFollow, follow)
    applyDataToForm(_formAnimation1, animation1)
    applyDataToForm(_formAnimation2, animation2)
    applyDataToForm(_formAnimation3, animation3)
    applyDataToForm(_formTransitionIn, transitionIn)
    applyDataToForm(_formTransitionOut, transitionOut)
    applyDataToForm(_formTextarea, textarea)

    function applyDataToForm(form, data) {
        for(const key in data) {
            const id = `${form.id}-${key}`
            const el = document.querySelector(`#${id}`)
            if(typeof data[key] != 'object') {
                if(el.type == 'checkbox') {
                    el.checked = data[key] ?? ''
                } else {
                    el.value = data[key]
                }
            }
        }
    }
}

function getIndentationString(count, size) {
    return count == 0 
        ? ''
        : new Array(count+1).join(
            size == 0 
                ? ''
                : new Array(size+1).join(' ') 
        )
}

function renderJS(value, key, indentCount) {
    const indentSize = parseInt(_formSubmit.querySelector('#formSubmit-indentation').value)
    const indentStr = getIndentationString(indentCount, indentSize)
    const keyStr = key == null 
        ? ''
        : key.includes('-') 
            ? `'${key}': `
            : `${key}: `
    let result = ""
    if(value === null || typeof value == 'undefined') {
        result += `${indentStr}${keyStr}null`
    } else if(Array.isArray(value)) {
        result += `${indentStr}${keyStr}[`
        const resultArr = []
        for(v of value) {
            resultArr.push(renderJS(v, null, indentCount+1))
        }
        result += resultArr.length == 0 
            ? ']' 
            : `\n${resultArr.join(',\n')}${indentStr}\n${indentStr}]`
    } else {
        const floatValue = parseFloat(value)
        const boolValue = value == 'true' 
            ? true
            : value == 'false'
                ? false
                : null                
        if(!isNaN(floatValue)) { 
            result += `${indentStr}${keyStr}${parseFloat(value)}`
        } else if (boolValue !== null) {
            result += `${indentStr}${keyStr}${value ? 'true' : 'false'}`
        } else if(typeof value == 'string') {
            result += `${indentStr}${keyStr}'${value}'`
        } else if(typeof value == 'object') {
            result += `${indentStr}${keyStr}{`
                const resultArr = []
                for (const [p, val] of Object.entries(value)) {
                    resultArr.push(renderJS(val, p, indentCount+1))
                }
                result += resultArr.length == 0 
                    ? '}' 
                    : `\n${resultArr.join(',\n')}${indentStr}\n${indentStr}}`
        } else {
            result += `${indentStr}${keyStr}undefined`
        }
    }
    return result
}