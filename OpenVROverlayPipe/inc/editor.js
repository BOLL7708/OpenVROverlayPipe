class Editor {
    constructor() {
        // Websocket 
        this._ws = null;
        this._wsActive = false;
        this._localStorageConfigs = 'OpenVRNotificationPipe.BOLL7708.Config';
        this._localStorageConfigNames = 'OpenVRNotificationPipe.BOLL7708.ConfigNames';
        this._localStorageLastConfigName = 'OpenVRNotificationPipe.BOLL7708.LastConfigName';
        // Construct forms
        this._formSubmit = document.querySelector('#formSubmit');
        this._formConfig = document.querySelector('#formConfig');
        this._formImage = document.querySelector('#formImage');
        this._formProperties = document.querySelector('#formProperties');
        this._formFollow = document.querySelector('#formFollow');
        this._templateAnimation = document.querySelector('#templateAnimation');
        this._formAnimation1 = document.querySelector('#formAnimation1');
        this._formAnimation2 = document.querySelector('#formAnimation2');
        this._formAnimation3 = document.querySelector('#formAnimation3');
        this._templateTransition = document.querySelector('#templateTransition');
        this._formTransitionIn = document.querySelector('#formTransitionIn');
        this._formTransitionOut = document.querySelector('#formTransitionOut');
        this._formTextarea = document.querySelector('#formTextarea');
        this._templateTween = document.querySelector('#templateTween');
        // General elements
        this._submit = document.querySelector('#submit');
        this._loadConfig = document.querySelector('#loadConfig');
        this._saveConfig = document.querySelector('#saveConfig');
        this._deleteConfig = document.querySelector('#deleteConfig');
        // File elements
        this._imgData = null;
        this._image = document.querySelector('#image');
        this._file = document.querySelector('#file');
        // Config elements
        this._config = document.querySelector('#config');
        this._copyJSON = document.querySelector('#copyJSON');
        this._downloadJSON = document.querySelector('#downloadJSON');
        this._copyJS = document.querySelector('#copyJS');
        this._downloadJS = document.querySelector('#downloadJS');
    }
    // Program loop
    init() {
        // Key listener
        document.onkeyup = function (e) {
            switch (e.code) {
                case 'NumpadEnter':
                case 'Enter':
                    this.sendNotification.call(this, e);
                    break;
                default:
                    // console.log(e.code)
                    break;
            }
        }.bind(this);
        // Clone forms from templates
        this._formFollow.appendChild(this._templateTween.content.cloneNode(true));
        this._formAnimation1.appendChild(this._templateAnimation.content.cloneNode(true));
        this._formAnimation2.appendChild(this._templateAnimation.content.cloneNode(true));
        this._formAnimation3.appendChild(this._templateAnimation.content.cloneNode(true));
        this._formTransitionIn.appendChild(this._templateTransition.content.cloneNode(true));
        this._formTransitionIn.appendChild(this._templateTween.content.cloneNode(true));
        this._formTransitionOut.appendChild(this._templateTransition.content.cloneNode(true));
        this._formTransitionOut.appendChild(this._templateTween.content.cloneNode(true));
        // Modify the DOM by adding labels and renaming IDs
        document.querySelectorAll('input, select, canvas').forEach(el => {
            var _a;
            // ID
            const form = el.parentElement;
            const name = (_a = el.name) !== null && _a !== void 0 ? _a : el.id;
            const id = `${form.id}-${name}`;
            el.id = id;
            // Wrapping + label
            const wrapper = document.createElement('p');
            el.parentNode.insertBefore(wrapper, el);
            const label = document.createElement('label');
            label.setAttribute('for', id);
            label.innerHTML = name.toLowerCase() == 'tweentype'
                ? `<a href="https://easings.net/" target="_blank">${name}</a>`
                : `${name}:`;
            wrapper.appendChild(label);
            wrapper.appendChild(el);
        });
        // Add event listeners
        this._submit.addEventListener('click', this.sendNotification.bind(this));
        this._loadConfig.addEventListener('click', this.loadConfig.bind(this));
        this._saveConfig.addEventListener('click', this.saveConfig.bind(this));
        this._deleteConfig.addEventListener('click', this.deleteConfig.bind(this));
        this._file.addEventListener('change', this.readImage.bind(this));
        this._copyJSON.addEventListener('click', this.copyConfigJSON.bind(this));
        this._downloadJSON.addEventListener('click', this.downloadConfigJSON.bind(this));
        this._copyJS.addEventListener('click', this.copyConfigJS.bind(this));
        this._downloadJS.addEventListener('click', this.downloadConfigJS.bind(this));
        this._configName = document.querySelector('#formConfig-configName');
        this._configList = document.querySelector('#formConfig-configList');
        this._configList.addEventListener('change', this.setCurrentConfigName.bind(this));
        this._configList.addEventListener('focus', (e) => { this._configList.selectedIndex = -1; });
        this.loadConfigNames();
        this.connectLoop();
    }
    connectLoop() {
        var _a;
        if (!this._wsActive) {
            const url = new URL(window.location.href);
            const params = new URLSearchParams(url.search);
            const port = (_a = params.get('port')) !== null && _a !== void 0 ? _a : 7711;
            // TODO: Save port in local data
            var wsUri = `ws://localhost:${port}`;
            this._wsActive = true;
            if (this._ws != null)
                this._ws.close();
            try {
                this._ws = new WebSocket(wsUri);
                this._ws.onopen = function (evt) {
                    this._wsActive = true;
                    document.title = 'Pipe Editor - CONNECTED';
                    this._submit.disabled = false;
                }.bind(this);
                this._ws.onclose = function (evt) {
                    this._wsActive = false;
                    document.title = 'Pipe Editor - DISCONNECTED';
                    this._submit.disabled = true;
                }.bind(this);
                this._ws.onmessage = function (evt) {
                    const data = JSON.parse(evt.data);
                    console.log(data);
                }.bind(this);
                this._ws.onerror = function (evt) {
                    this._wsActive = false;
                    console.error(`WebSocket Connection Error`);
                    document.title = `Pipe Editor - ERROR`;
                }.bind(this);
            }
            catch (error) {
                console.error(`WebSocket Init Error: ${error}`);
                this._wsActive = false;
                document.title = `Pipe Editor - ERROR`;
            }
        }
        setTimeout(this.connectLoop.bind(this), 5000);
    }
    // Image functions
    readImage() {
        this._formImage.classList.remove('unset');
        const files = this._file.files;
        if (files && files[0]) {
            var FR = new FileReader();
            FR.onload = function (e) {
                this._image.src = FR.result;
                this._imgData = FR.result.toString().split(',')[1];
            }.bind(this);
            FR.readAsDataURL(files[0]);
        }
    }
    getImageData() {
        return this._imgData;
    }
    getImagePath() {
        var _a, _b;
        return 'C:/replace/with/path/on/disk/' + ((_b = (_a = this._file.files[0]) === null || _a === void 0 ? void 0 : _a.name) !== null && _b !== void 0 ? _b : 'image.png');
    }
    // Data
    sendNotification(e) {
        e === null || e === void 0 ? void 0 : e.preventDefault();
        const data = this.getData();
        // Send data
        this._ws.send(JSON.stringify(data));
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
        };
        // Get all form data
        const propertiesData = new FormData(this._formProperties);
        const propertiesJSON = Object.fromEntries(propertiesData.entries());
        propertiesJSON.anchorType = propertiesData.getAll('anchorType').pop();
        const followData = new FormData(this._formFollow);
        const followJSON = Object.fromEntries(followData.entries());
        followJSON.tweenType = followData.getAll('tweenType').pop();
        const animationData1 = new FormData(this._formAnimation1);
        const animationJSON1 = Object.fromEntries(animationData1.entries());
        animationJSON1.property = animationData1.getAll('property').pop();
        animationJSON1.phase = animationData1.getAll('phase').pop();
        animationJSON1.waveform = animationData1.getAll('waveform').pop();
        const animationData2 = new FormData(this._formAnimation2);
        const animationJSON2 = Object.fromEntries(animationData2.entries());
        animationJSON2.property = animationData2.getAll('property').pop();
        animationJSON2.phase = animationData2.getAll('phase').pop();
        animationJSON2.waveform = animationData2.getAll('waveform').pop();
        const animationData3 = new FormData(this._formAnimation3);
        const animationJSON3 = Object.fromEntries(animationData3.entries());
        animationJSON3.property = animationData3.getAll('property').pop();
        animationJSON3.phase = animationData3.getAll('phase').pop();
        animationJSON3.waveform = animationData3.getAll('waveform').pop();
        const transitionsInData = new FormData(this._formTransitionIn);
        const transitionsInJSON = Object.fromEntries(transitionsInData.entries());
        transitionsInJSON.tweenType = transitionsInData.getAll('tweenType').pop();
        const transitionsOutData = new FormData(this._formTransitionOut);
        const transitionsOutJSON = Object.fromEntries(transitionsOutData.entries());
        transitionsOutJSON.tweenType = transitionsOutData.getAll('tweenType').pop();
        const textareaData = new FormData(this._formTextarea);
        const textareaJSON = Object.fromEntries(textareaData.entries());
        textareaJSON.horizontalAlignment = textareaData.getAll('horizontalAlignment').pop();
        textareaJSON.verticalAlignment = textareaData.getAll('verticalAlignment').pop();
        // Fill data
        data.customProperties = propertiesJSON;
        data.customProperties['follow'] = {};
        if (followJSON.enabled)
            data.customProperties['follow'] = followJSON;
        data.customProperties['animations'] = [];
        if (animationJSON1.property != '0')
            data.customProperties['animations'].push(animationJSON1);
        if (animationJSON2.property != '0')
            data.customProperties['animations'].push(animationJSON2);
        if (animationJSON3.property != '0')
            data.customProperties['animations'].push(animationJSON3);
        data.customProperties['transitions'] = [];
        data.customProperties['transitions'].push(transitionsInJSON);
        data.customProperties['transitions'].push(transitionsOutJSON);
        data.customProperties['textAreas'] = [];
        if (textareaJSON.text.toString().length > 0)
            data.customProperties['textAreas'].push(textareaJSON);
        return data;
    }
    copyConfigJSON(e) {
        e === null || e === void 0 ? void 0 : e.preventDefault();
        const data = this.getData();
        data.imageData = '';
        this._config.innerHTML = JSON.stringify(data, null, 4);
        this._config.select();
        document.execCommand('copy');
    }
    downloadConfigJSON(e) {
        e === null || e === void 0 ? void 0 : e.preventDefault();
        const data = this.getData();
        data.imageData = '';
        const json = JSON.stringify(data, null, 4);
        this.download(json, 'pipe-config.json', 'text/plain');
    }
    copyConfigJS(e) {
        e === null || e === void 0 ? void 0 : e.preventDefault();
        const data = this.getData();
        data.imageData = '';
        this._config.innerHTML = this.renderJS(data, null, 0);
        this._config.select();
        document.execCommand('copy');
    }
    downloadConfigJS(e) {
        e === null || e === void 0 ? void 0 : e.preventDefault();
        const data = this.getData();
        data.imageData = '';
        const json = this.renderJS(data, null, 0);
        this.download(json, 'pipe-config.js', 'text/plain');
    }
    // Function to download data to a file https://stackoverflow.com/a/30832210
    download(data, filename, type) {
        const file = new Blob([data], { type: type });
        const a = document.createElement("a");
        const url = URL.createObjectURL(file);
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        setTimeout(function () {
            document.body.removeChild(a);
            window.URL.revokeObjectURL(url);
        }, 0);
    }
    loadConfig(e) {
        var _a, _b, _c, _d, _e, _f, _g, _h, _j;
        e === null || e === void 0 ? void 0 : e.preventDefault();
        const configName = (_a = this._configName.value) !== null && _a !== void 0 ? _a : '';
        if (configName.length > 0) {
            const json = window.localStorage.getItem(this.getConfigKey(configName));
            window.localStorage.setItem(this._localStorageLastConfigName, configName);
            const data = JSON.parse(json);
            if (data == null)
                return alert(`Config "${configName}" not found`);
            const properties = (_b = data.customProperties) !== null && _b !== void 0 ? _b : {};
            const follow = (_c = data.customProperties.follow) !== null && _c !== void 0 ? _c : {};
            const animation1 = (_d = data.customProperties.animations[0]) !== null && _d !== void 0 ? _d : {};
            const animation2 = (_e = data.customProperties.animations[1]) !== null && _e !== void 0 ? _e : {};
            const animation3 = (_f = data.customProperties.animations[2]) !== null && _f !== void 0 ? _f : {};
            const transitionIn = (_g = data.customProperties.transitions[0]) !== null && _g !== void 0 ? _g : {};
            const transitionOut = (_h = data.customProperties.transitions[1]) !== null && _h !== void 0 ? _h : {};
            const textarea = (_j = data.customProperties.textAreas[0]) !== null && _j !== void 0 ? _j : {};
            applyDataToForm(this._formProperties, properties);
            applyDataToForm(this._formFollow, follow);
            applyDataToForm(this._formAnimation1, animation1);
            applyDataToForm(this._formAnimation2, animation2);
            applyDataToForm(this._formAnimation3, animation3);
            applyDataToForm(this._formTransitionIn, transitionIn);
            applyDataToForm(this._formTransitionOut, transitionOut);
            applyDataToForm(this._formTextarea, textarea);
            function applyDataToForm(form, data) {
                var _a;
                for (const key in data) {
                    const id = `${form.id}-${key}`;
                    const el = document.querySelector(`#${id}`);
                    if (typeof data[key] != 'object') {
                        if (el['type'] == 'checkbox') {
                            el['checked'] = (_a = data[key]) !== null && _a !== void 0 ? _a : '';
                        }
                        else {
                            el['value'] = data[key];
                        }
                    }
                }
            }
            alert(`Config "${configName} loaded"`);
        }
        else {
            alert('Please enter a name for your config');
        }
    }
    saveConfig(e) {
        e === null || e === void 0 ? void 0 : e.preventDefault();
        const configName = this._configName.value;
        if (configName.length > 0) {
            const data = JSON.stringify(this.getData());
            window.localStorage.setItem(this.getConfigKey(configName), data);
            window.localStorage.setItem(this._localStorageLastConfigName, configName);
            this.saveConfigName(configName);
            alert(`Config "${configName}" saved`);
        }
        else {
            alert('Please enter a name for your config');
        }
    }
    deleteConfig(e) {
        e === null || e === void 0 ? void 0 : e.preventDefault();
        const configName = this._configName.value;
        if (configName.length > 0) {
            const doIt = confirm(`Are you sure you want to delete config "${configName}"?`);
            if (doIt) {
                window.localStorage.removeItem(this.getConfigKey(configName));
                window.localStorage.setItem(this._localStorageLastConfigName, '');
                this.deleteConfigName(configName);
                alert(`Config "${configName}" deleted`);
            }
        }
        else {
            alert('Please enter a name for your config');
        }
    }
    setCurrentConfigName(e) {
        e === null || e === void 0 ? void 0 : e.preventDefault();
        const name = this._configList.value;
        this._configName.value = name;
        window.localStorage.setItem(this._localStorageLastConfigName, name);
    }
    loadConfigNames() {
        var _a;
        const configNames = window.localStorage.getItem(this._localStorageConfigNames);
        const lastConfigName = window.localStorage.getItem(this._localStorageLastConfigName);
        const names = (_a = JSON.parse(configNames)) !== null && _a !== void 0 ? _a : [''];
        this._configName.value = lastConfigName !== null && lastConfigName !== void 0 ? lastConfigName : '';
        this.applyConfigNamesToSelect(names, lastConfigName);
    }
    saveConfigName(name) {
        var _a;
        const namesData = window.localStorage.getItem(this._localStorageConfigNames);
        const names = (_a = JSON.parse(namesData)) !== null && _a !== void 0 ? _a : [];
        if (!names.includes(name))
            names.push(name);
        window.localStorage.setItem(this._localStorageConfigNames, JSON.stringify(names));
        this.applyConfigNamesToSelect(names, name);
    }
    deleteConfigName(name) {
        var _a;
        const namesData = window.localStorage.getItem(this._localStorageConfigNames);
        let names = (_a = JSON.parse(namesData)) !== null && _a !== void 0 ? _a : [];
        if (names.includes(name))
            delete names[names.indexOf(name)];
        names = names.filter(n => n != null);
        window.localStorage.setItem(this._localStorageConfigNames, JSON.stringify(names));
        this.applyConfigNamesToSelect(names, name);
        this._configName.value = '';
    }
    applyConfigNamesToSelect(names, selected = null) {
        this._configList.innerHTML = '';
        for (const name of names) {
            const option = document.createElement('option');
            option.value = name;
            option.innerText = name;
            if (name == selected)
                option.selected = true;
            this._configList.appendChild(option);
        }
    }
    getConfigKey(name) {
        return `${this._localStorageConfigs}-${name}`;
    }
    getIndentationString(count, size) {
        return count == 0
            ? ''
            : new Array(count + 1).join(size == 0
                ? ''
                : new Array(size + 1).join(' '));
    }
    renderJS(value, key, indentCount) {
        const indentStr = this.getIndentationString(indentCount, 4);
        const keyStr = key == null
            ? ''
            : key.includes('-')
                ? `'${key}': `
                : `${key}: `;
        let result = "";
        if (value === null || typeof value == 'undefined') {
            result += `${indentStr}${keyStr}null`;
        }
        else if (Array.isArray(value)) {
            result += `${indentStr}${keyStr}[`;
            const resultArr = [];
            for (const v of value) {
                resultArr.push(this.renderJS(v, null, indentCount + 1));
            }
            result += resultArr.length == 0
                ? ']'
                : `\n${resultArr.join(',\n')}${indentStr}\n${indentStr}]`;
        }
        else {
            const floatValue = parseFloat(value);
            const boolValue = value == 'true'
                ? true
                : value == 'false'
                    ? false
                    : null;
            if (!isNaN(floatValue)) {
                result += `${indentStr}${keyStr}${parseFloat(value)}`;
            }
            else if (boolValue !== null) {
                result += `${indentStr}${keyStr}${value ? 'true' : 'false'}`;
            }
            else if (typeof value == 'string') {
                result += `${indentStr}${keyStr}'${value}'`;
            }
            else if (typeof value == 'object') {
                result += `${indentStr}${keyStr}{`;
                const resultArr = [];
                for (const [p, val] of Object.entries(value)) {
                    resultArr.push(this.renderJS(val, p, indentCount + 1));
                }
                result += resultArr.length == 0
                    ? '}'
                    : `\n${resultArr.join(',\n')}${indentStr}\n${indentStr}}`;
            }
            else {
                result += `${indentStr}${keyStr}undefined`;
            }
        }
        return result;
    }
}
//# sourceMappingURL=editor.js.map