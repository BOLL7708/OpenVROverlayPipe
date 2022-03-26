class Editor {
    // Websocket 
    private _ws = null
    private _wsActive = false
    private _localStorageKey = 'OpenVRNotificationPipe.BOLL7708.Config'

    // Construct forms
    private _formSubmit = document.querySelector<HTMLFormElement>('#formSubmit')
    private _formImage = document.querySelector<HTMLFormElement>('#formImage')
    private _formProperties = document.querySelector<HTMLFormElement>('#formProperties')
    private _formFollow = document.querySelector<HTMLFormElement>('#formFollow')
    private _templateAnimation = document.querySelector<HTMLTemplateElement>('#templateAnimation')
    private _formAnimation1 = document.querySelector<HTMLFormElement>('#formAnimation1')
    private _formAnimation2 = document.querySelector<HTMLFormElement>('#formAnimation2')
    private _formAnimation3 = document.querySelector<HTMLFormElement>('#formAnimation3')
    private _templateTransition = document.querySelector<HTMLTemplateElement>('#templateTransition')
    private _formTransitionIn = document.querySelector<HTMLFormElement>('#formTransitionIn')
    private _formTransitionOut = document.querySelector<HTMLFormElement>('#formTransitionOut')
    private _formTextarea = document.querySelector<HTMLFormElement>('#formTextarea')
    private _templateTween = document.querySelector<HTMLTemplateElement>('#templateTween')
    private _formConfig = document.querySelector<HTMLTemplateElement>('#formConfig')

    // General elements
    private _submit = document.querySelector<HTMLButtonElement>('#submit')    

    // File elements
    private _imgData = null;
    private _image = document.querySelector<HTMLImageElement>('#image')
    private _file = document.querySelector<HTMLInputElement>('#file')

    // Config elements
    private _config = document.querySelector<HTMLTextAreaElement>('#config')
    private _copyJSON = document.querySelector<HTMLButtonElement>('#copyJSON')
    private _downloadJSON = document.querySelector<HTMLButtonElement>('#downloadJSON')
    private _copyJS = document.querySelector<HTMLButtonElement>('#copyJS')
    private _downloadJS = document.querySelector<HTMLButtonElement>('#downloadJS')

    // Program loop
    init()
    {
        // Key listener
        document.onkeyup = function (e: KeyboardEvent) {
            switch(e.code) {
                case 'NumpadEnter':
                case 'Enter': 
                    this.sendNotification.call(this, e)
                    break
                default: 
                    // console.log(e.code)
                    break
            }
        }.bind(this)

        // Clone forms from templates
        this._formFollow.appendChild(this._templateTween.content.cloneNode(true))
        this._formAnimation1.appendChild(this._templateAnimation.content.cloneNode(true))
        this._formAnimation2.appendChild(this._templateAnimation.content.cloneNode(true))
        this._formAnimation3.appendChild(this._templateAnimation.content.cloneNode(true))
        this._formTransitionIn.appendChild(this._templateTransition.content.cloneNode(true))
        this._formTransitionIn.appendChild(this._templateTween.content.cloneNode(true))
        this._formTransitionOut.appendChild(this._templateTransition.content.cloneNode(true))
        this._formTransitionOut.appendChild(this._templateTween.content.cloneNode(true))

        // Modify the DOM by adding labels and renaming IDs
        document.querySelectorAll('input, select, canvas').forEach(el => {
            // ID
            const form = el.parentElement
            const name = (<HTMLFormElement> el).name ?? el.id
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

        // Add event listeners
        this._submit.addEventListener('click', this.sendNotification.bind(this))
        this._file.addEventListener('change', this.readImage.bind(this))
        this._copyJSON.addEventListener('click', this.copyConfigJSON.bind(this))
        this._downloadJSON.addEventListener('click', this.downloadConfigJSON.bind(this))
        this._copyJS.addEventListener('click', this.copyConfigJS.bind(this))
        this._downloadJS.addEventListener('click', this.downloadConfigJS.bind(this))

        this.connectLoop()
        /*
        // TODO: Make this an option.
        _config.innerHTML = localStorage.getItem(_localStorageKey) ?? ''
        if(_config.innerHTML.length > 0) {
            loadConfig()
        }
        */
    }
    connectLoop() 
    {
        if(!this._wsActive) {
            const url = new URL(window.location.href)
            const params = new URLSearchParams(url.search)
            const port = params.get('port') ?? 8077

            // TODO: Save port in local data

            var wsUri = `ws://localhost:${port}`
            this._wsActive = true
            if(this._ws != null) this._ws.close()
            try {
                this._ws = new WebSocket(wsUri)
                this._ws.onopen = function(evt) { 
                    this._wsActive = true
                    document.title = 'Pipe Editor - CONNECTED'
                    this._submit.disabled = false
                }.bind(this)
                this._ws.onclose = function(evt) { 
                    this._wsActive = false
                    document.title = 'Pipe Editor - DISCONNECTED'
                    this._submit.disabled = true
                }.bind(this)
                this._ws.onmessage = function(evt) {
                    const data = JSON.parse(evt.data)
                    console.log(data)
                }.bind(this)
                this._ws.onerror = function(evt) { 
                    this._wsActive = false
                    console.error(`WebSocket Connection Error`)
                    document.title = `Pipe Editor - ERROR`
                }.bind(this)
            } catch (error) {
                console.error(`WebSocket Init Error: ${error}`)
                this._wsActive = false
                document.title = `Pipe Editor - ERROR`
            }
        }
        setTimeout(this.connectLoop.bind(this), 5000);
    }

    // Image functions
    readImage() {
        this._formImage.classList.remove('unset')
        const files = this._file.files
        if(files && files[0]) {
            var FR = new FileReader()
            FR.onload = function(e: ProgressEvent) {
                this._image.src = FR.result
                this._imgData = FR.result.toString().split(',')[1]
            }.bind(this)
            FR.readAsDataURL( files[0] )
        }
    }
    getImageData() {
        return this._imgData
    }
    getImagePath() {
        return 'C:/replace/with/path/on/disk/'+this._file.files[0].name
    }

    // Data
    sendNotification(e: Event) {
        e?.preventDefault()
        const data = this.getData()
        // Send data
        this._ws.send(JSON.stringify(data))
        
        // TODO: Change be a config load/save management system
        // window.localStorage.setItem(_localStorageKey, JSON.stringify(data))
    }

    getData() {
        const data = {
            // General
            imageData: this.getImageData(),
            imagePath: this.getImagePath(),
            
            // Standard
            basicTitle: "",
            basicMessage: "",

            // Custom
            customProperties: {}
        }

        // Get all form data
        const propertiesData = new FormData(this._formProperties)
        const propertiesJSON = Object.fromEntries(propertiesData.entries())
        propertiesJSON.anchorType = propertiesData.getAll('anchorType').pop()

        const followData = new FormData(this._formFollow)
        const followJSON = Object.fromEntries(followData.entries())
        followJSON.tweenType = followData.getAll('tweenType').pop()

        const animationData1 = new FormData(this._formAnimation1)
        const animationJSON1 = Object.fromEntries(animationData1.entries())
        animationJSON1.property = animationData1.getAll('property').pop()
        animationJSON1.phase = animationData1.getAll('phase').pop()
        animationJSON1.waveform = animationData1.getAll('waveform').pop()

        const animationData2 = new FormData(this._formAnimation2)
        const animationJSON2 = Object.fromEntries(animationData2.entries())
        animationJSON2.property = animationData2.getAll('property').pop()
        animationJSON2.phase = animationData2.getAll('phase').pop()
        animationJSON2.waveform = animationData2.getAll('waveform').pop()

        const animationData3 = new FormData(this._formAnimation3)
        const animationJSON3 = Object.fromEntries(animationData3.entries())
        animationJSON3.property = animationData3.getAll('property').pop()
        animationJSON3.phase = animationData3.getAll('phase').pop()
        animationJSON3.waveform = animationData3.getAll('waveform').pop()

        const transitionsInData = new FormData(this._formTransitionIn)
        const transitionsInJSON = Object.fromEntries(transitionsInData.entries())
        transitionsInJSON.tweenType = transitionsInData.getAll('tweenType').pop()

        const transitionsOutData = new FormData(this._formTransitionOut)
        const transitionsOutJSON = Object.fromEntries(transitionsOutData.entries())
        transitionsOutJSON.tweenType = transitionsOutData.getAll('tweenType').pop()

        const textareaData = new FormData(this._formTextarea)
        const textareaJSON = Object.fromEntries(textareaData.entries())
        textareaJSON.horizontalAlignment = textareaData.getAll('horizontalAlignment').pop()
        textareaJSON.verticalAlignment = textareaData.getAll('verticalAlignment').pop()

        // Fill data
        data.customProperties = propertiesJSON
        data.customProperties['follow'] = {}
        if(followJSON.enabled) data.customProperties['follow'] = followJSON
        data.customProperties['animations'] = []
        if(animationJSON1.property != '0') data.customProperties['animations'].push(animationJSON1)
        if(animationJSON2.property != '0') data.customProperties['animations'].push(animationJSON2)
        if(animationJSON3.property != '0') data.customProperties['animations'].push(animationJSON3)
        data.customProperties['transitions'] = []
        data.customProperties['transitions'].push(transitionsInJSON)
        data.customProperties['transitions'].push(transitionsOutJSON)
        data.customProperties['textAreas'] = []
        if(textareaJSON.text.toString().length > 0) data.customProperties['textAreas'].push(textareaJSON)
        return data
    }

    copyConfigJSON(e: Event) {
        e?.preventDefault()
        const indent = this._formSubmit.querySelector<HTMLInputElement>('#formSubmit-indentation').value
        const data = this.getData()
        data.imageData = ''
        this._config.innerHTML = JSON.stringify(data, null, parseInt(indent))
        this._config.select()
        document.execCommand('copy')
    }

    downloadConfigJSON(e: Event) {
        e?.preventDefault()
        const indent = this._formSubmit.querySelector<HTMLInputElement>('#formSubmit-indentation').value
        const data = this.getData()
        data.imageData = ''
        const json = JSON.stringify(data, null, parseInt(indent))
        this.download(json, 'pipe-config.json', 'text/plain')
    }

    copyConfigJS(e: Event) {
        e?.preventDefault()
        const data = this.getData()
        data.imageData = ''
        this._config.innerHTML = this.renderJS(data, null, 0)
        this._config.select()
        document.execCommand('copy')
    }

    downloadConfigJS(e: Event) {
        e?.preventDefault()
        const data = this.getData()
        data.imageData = ''
        const json = this.renderJS(data, null, 0)
        this.download(json, 'pipe-config.js', 'text/plain')
    }

    // Function to download data to a file https://stackoverflow.com/a/30832210
    download(data: string, filename: string, type: string) {
        const file = new Blob([data], {type: type});
        const a = document.createElement("a")
        const url = URL.createObjectURL(file)
        a.href = url
        a.download = filename
        document.body.appendChild(a)
        a.click()
        setTimeout(function() {
            document.body.removeChild(a)
            window.URL.revokeObjectURL(url)
        }, 0)
    }

    loadConfig(e: Event) {
        e?.preventDefault()
        const data = JSON.parse(this._config.value)
        if(this._config.value.length > 0) window.localStorage.setItem(this._localStorageKey, this._config.value)
        const properties = data.customProperties ?? {}
        const follow = data.customProperties.follow ?? {}
        const animation1 = data.customProperties.animations[0] ?? {}
        const animation2 = data.customProperties.animations[1] ?? {}
        const animation3 = data.customProperties.animations[2] ?? {}
        const transitionIn = data.customProperties.transitions[0] ?? {}
        const transitionOut = data.customProperties.transitions[1] ?? {}
        const textarea = data.customProperties.textAreas[0] ?? {}

        applyDataToForm(this._formProperties, properties)
        applyDataToForm(this._formFollow, follow)
        applyDataToForm(this._formAnimation1, animation1)
        applyDataToForm(this._formAnimation2, animation2)
        applyDataToForm(this._formAnimation3, animation3)
        applyDataToForm(this._formTransitionIn, transitionIn)
        applyDataToForm(this._formTransitionOut, transitionOut)
        applyDataToForm(this._formTextarea, textarea)

        function applyDataToForm(form, data) {
            for(const key in data) {
                const id = `${form.id}-${key}`
                const el = document.querySelector(`#${id}`)
                if(typeof data[key] != 'object') {
                    if(el['type'] == 'checkbox') {
                        el['checked'] = data[key] ?? ''
                    } else {
                        el['value'] = data[key]
                    }
                }
            }
        }
    }

    getIndentationString(count: number, size: number) {
        return count == 0 
            ? ''
            : new Array(count+1).join(
                size == 0 
                    ? ''
                    : new Array(size+1).join(' ') 
            )
    }

    renderJS(value: any, key: string, indentCount: number) {
        const indentSize = parseInt(this._formSubmit.querySelector<HTMLInputElement>('#formSubmit-indentation').value)
        const indentStr = this.getIndentationString(indentCount, indentSize)
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
            for(const v of value) {
                resultArr.push(this.renderJS(v, null, indentCount+1))
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
                        resultArr.push(this.renderJS(val, p, indentCount+1))
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
}