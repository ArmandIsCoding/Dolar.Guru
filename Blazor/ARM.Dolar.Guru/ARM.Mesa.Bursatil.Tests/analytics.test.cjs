const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');

const source = fs.readFileSync(path.join(__dirname,
    '../ARM.Mesa.Bursatil.BaseBlazor/wwwroot/js/analytics.js'), 'utf8');

function setup(hostname, measurementId = 'G-5FH8L0YFGE') {
    const scripts = [];
    const context = vm.createContext({
        window: { location: { hostname } },
        document: {
            currentScript: { dataset: { measurementId } },
            createElement: () => ({}),
            head: { appendChild: script => scripts.push(script) }
        }
    });
    return { context, scripts, run: () => vm.runInContext(source, context) };
}

for (const host of ['mesabursatil.ar', 'www.mesabursatil.ar']) {
    test(`initializes exactly once on ${host}`, () => {
        const { context, scripts, run } = setup(host);
        run();
        run();
        assert.equal(scripts.length, 1);
        assert.equal(scripts[0].src, 'https://www.googletagmanager.com/gtag/js?id=G-5FH8L0YFGE');
        assert.equal(scripts[0].async, true);
        const commands = context.window.dataLayer;
        assert.equal(commands.length, 2);
        assert.equal(commands[0][0], 'js');
        assert.equal(commands[1][0], 'config');
        assert.equal(commands[1][1], 'G-5FH8L0YFGE');
        assert.equal(commands[1][2].allow_google_signals, false);
        assert.equal(commands[1][2].allow_ad_personalization_signals, false);
    });
}

for (const host of ['localhost', '127.0.0.1', '181.14.210.127', 'preview.mesabursatil.ar', 'mesabursatil.ar.example.com']) {
    test(`does not collect on ${host}`, () => {
        const { context, scripts, run } = setup(host);
        run();
        assert.equal(scripts.length, 0);
        assert.equal(context.window.dataLayer, undefined);
    });
}

for (const id of ['', 'invalid', 'G-ABC&other=value']) {
    test(`rejects invalid measurement ID: ${id}`, () => {
        const { scripts, run } = setup('mesabursatil.ar', id);
        run();
        assert.equal(scripts.length, 0);
    });
}
