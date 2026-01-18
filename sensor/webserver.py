# webserver.py - Web server s Microdot

from lib.microdot import Microdot, Response
from lib.microdot.auth import BasicAuth
import json
import machine
import gc

from env import WEB_USERNAME, WEB_PASSWORD, STUDENT_ID
from constants import APP_VERSION, SETTINGS_FILE, Color
from helpers import get_reset_cause, validate_host_address

# Globálna referencia na device
_device = None

# Vytvor aplikáciu
app = Microdot()
Response.default_content_type = 'text/html'

# Basic Auth
auth = BasicAuth()


@auth.authenticate
def verify_password(request, username, password):
    if username == WEB_USERNAME and password == WEB_PASSWORD:
        return username
    return None


# HTML šablóny
def render_template(title, content, message=None, is_error=False):
    msg_html = ""
    if message:
        cls = "error" if is_error else "success"
        msg_html = f'<div class="{cls}">{message}</div>'

    return f"""<!DOCTYPE html>
<html>
<head>
    <title>{title}</title>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; background: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        h1 {{ color: #333; }}
        nav {{ margin-bottom: 20px; padding-bottom: 10px; border-bottom: 1px solid #eee; }}
        nav a {{ margin-right: 15px; color: #007bff; text-decoration: none; }}
        nav a:hover {{ text-decoration: underline; }}
        input, select {{ width: 100%; padding: 8px; margin: 5px 0 15px 0; border: 1px solid #ddd; border-radius: 4px; box-sizing: border-box; }}
        button {{ background: #007bff; color: white; padding: 10px 20px; border: none; border-radius: 4px; cursor: pointer; margin: 5px 5px 5px 0; }}
        button:hover {{ background: #0056b3; }}
        .btn-danger {{ background: #dc3545; }}
        .btn-danger:hover {{ background: #c82333; }}
        .info {{ background: #e7f3ff; padding: 10px; border-radius: 4px; margin: 10px 0; }}
        .error {{ background: #ffe7e7; padding: 10px; border-radius: 4px; margin: 10px 0; color: #c00; }}
        .success {{ background: #e7ffe7; padding: 10px; border-radius: 4px; margin: 10px 0; color: #060; }}
        table {{ width: 100%; border-collapse: collapse; }}
        td {{ padding: 8px; border-bottom: 1px solid #eee; }}
        td:first-child {{ font-weight: bold; width: 40%; }}
    </style>
</head>
<body>
<div class="container">
    <nav>
        <a href="/">🏠 Info</a>
        <a href="/diagnostics">🔧 Diagnostika</a>
        <a href="/sysinfo">⚙️ Systém</a>
        <a href="/setup">🔐 Nastavenia</a>
    </nav>
    {msg_html}
    {content}
</div>
</body>
</html>"""


# Routes
@app.route('/')
async def index(request):
    """Hlavná info stránka"""
    global _device

    temp_str = "N/A"
    hum_str = "N/A"

    if _device and _device.sensor:
        try:
            _device.sensor.measure()
            temp_str = f"{_device.sensor.temperature()} °C"
            hum_str = f"{_device.sensor.humidity()} %"
        except:
            pass

    content = f"""
    <h1>🌡️ thsensor-{STUDENT_ID}</h1>
    <div class="info">
        <p>Inteligentný senzor teploty a vlhkosti</p>
        <p>Verzia: {APP_VERSION}</p>
    </div>
    <h2>Aktuálne hodnoty</h2>
    <table>
        <tr><td>Teplota</td><td>{temp_str}</td></tr>
        <tr><td>Vlhkosť</td><td>{hum_str}</td></tr>
    </table>
    """
    return render_template(f"thsensor-{STUDENT_ID}", content)


@app.route('/diagnostics')
async def diagnostics(request):
    """Diagnostická stránka"""
    global _device

    rows = ""

    # DHT22
    if _device and _device.sensor:
        try:
            _device.sensor.measure()
            temp = _device.sensor.temperature()
            hum = _device.sensor.humidity()
            rows += f"<tr><td>DHT22</td><td>✅ OK ({temp}°C, {hum}%)</td></tr>"
        except Exception as e:
            rows += f"<tr><td>DHT22</td><td>❌ Chyba: {e}</td></tr>"
    else:
        rows += "<tr><td>DHT22</td><td>⚠️ Nie je inicializovaný</td></tr>"

    # DS3231
    if _device and _device.rtc:
        try:
            t = _device.rtc.get_time()
            rows += f"<tr><td>DS3231 RTC</td><td>✅ OK ({t[4]:02d}:{t[5]:02d}:{t[6]:02d})</td></tr>"
        except Exception as e:
            rows += f"<tr><td>DS3231 RTC</td><td>❌ Chyba: {e}</td></tr>"
    else:
        rows += "<tr><td>DS3231 RTC</td><td>⚠️ Nie je pripojený</td></tr>"

    content = f"""
    <h1>🔧 Diagnostika</h1>
    <h2>Senzory</h2>
    <table>{rows}</table>

    <h2>Test LED</h2>
    <form method="POST" action="/led" style="display: flex; flex-wrap: wrap;">
        <button type="submit" name="color" value="red">🔴 Červená</button>
        <button type="submit" name="color" value="green">🟢 Zelená</button>
        <button type="submit" name="color" value="blue">🔵 Modrá</button>
        <button type="submit" name="color" value="off">⚫ Vypnúť</button>
    </form>
    """
    return render_template("Diagnostika", content)


@app.route('/led', methods=['POST'])
async def led_control(request):
    """Ovládanie LED"""
    global _device

    color = request.form.get('color', 'off')
    colors = {
        'red': Color.RED,
        'green': Color.GREEN,
        'blue': Color.BLUE,
        'off': Color.OFF
    }

    if _device:
        _device.led[0] = colors.get(color, Color.OFF)
        _device.led.write()

    return '', 302, {'Location': '/diagnostics'}


@app.route('/sysinfo')
async def sysinfo(request):
    """Systémové informácie"""
    gc.collect()
    free_mem = gc.mem_free()

    rtc = machine.RTC()
    dt = rtc.datetime()
    time_str = f"{dt[0]}-{dt[1]:02d}-{dt[2]:02d} {dt[4]:02d}:{dt[5]:02d}:{dt[6]:02d}"

    content = f"""
    <h1>⚙️ Systémové informácie</h1>
    <table>
        <tr><td>Verzia aplikácie</td><td>{APP_VERSION}</td></tr>
        <tr><td>Frekvencia CPU</td><td>{machine.freq() // 1000000} MHz</td></tr>
        <tr><td>Voľná pamäť</td><td>{free_mem} B</td></tr>
        <tr><td>Dôvod reštartu</td><td>{get_reset_cause()}</td></tr>
        <tr><td>Aktuálny čas</td><td>{time_str}</td></tr>
    </table>
    """
    return render_template("Systémové info", content)


@app.route('/setup')
@auth
async def setup(request):
    """Nastavenia - vyžaduje prihlásenie"""
    try:
        with open(SETTINGS_FILE, 'r') as f:
            settings = json.load(f)
    except:
        settings = {}

    mqtt = settings.get('mqtt', {})

    units_metric = "selected" if settings.get('units') == 'metric' else ""
    units_imperial = "selected" if settings.get('units') == 'imperial' else ""
    units_standard = "selected" if settings.get('units') == 'standard' else ""
    ssl_checked = "checked" if mqtt.get('ssl', False) else ""

    content = f"""
    <h1>🔐 Nastavenia</h1>
    <form method="POST" action="/setup">
        <h3>WiFi pripojenie</h3>
        <label>SSID:</label>
        <input type="text" name="wifi_ssid" value="{settings.get('wifi_ssid', '')}">
        <label>Heslo:</label>
        <input type="password" name="wifi_password" value="{settings.get('wifi_password', '')}">

        <h3>Čas</h3>
        <label>NTP server:</label>
        <input type="text" name="ntp_host" value="{settings.get('ntp_host', 'pool.ntp.org')}">

        <h3>Jednotky</h3>
        <label>Teplota:</label>
        <select name="units">
            <option value="metric" {units_metric}>Celsius (°C)</option>
            <option value="imperial" {units_imperial}>Fahrenheit (°F)</option>
            <option value="standard" {units_standard}>Kelvin (K)</option>
        </select>

        <h3>MQTT</h3>
        <label>Server:</label>
        <input type="text" name="mqtt_server" value="{mqtt.get('server', '')}">
        <label>Port:</label>
        <input type="number" name="mqtt_port" value="{mqtt.get('port', 8883)}">
        <label>Používateľ:</label>
        <input type="text" name="mqtt_user" value="{mqtt.get('user', '')}">
        <label>Heslo:</label>
        <input type="password" name="mqtt_password" value="{mqtt.get('password', '')}">
        <label>
            <input type="checkbox" name="mqtt_ssl" value="true" {ssl_checked}> SSL/TLS
        </label>

        <h3>MQTT Téma</h3>
        <label>Department:</label>
        <input type="text" name="mqtt_department" value="{mqtt.get('department', '')}">
        <label>Room:</label>
        <input type="text" name="mqtt_room" value="{mqtt.get('room', '')}">
        <label>ID:</label>
        <input type="text" name="mqtt_id" value="{mqtt.get('id', '')}">

        <button type="submit">Uložiť</button>
    </form>

    <h3>Reštart zariadenia</h3>
    <form method="POST" action="/restart">
        <button type="submit" class="btn-danger">Reštartovať</button>
    </form>
    """
    return render_template("Nastavenia", content)


@app.route('/setup', methods=['POST'])
@auth
async def setup_post(request):
    """Uloženie nastavení"""
    try:
        settings = {
            "units": request.form.get('units', 'metric'),
            "wifi_ssid": request.form.get('wifi_ssid', ''),
            "wifi_password": request.form.get('wifi_password', ''),
            "ntp_host": request.form.get('ntp_host', 'pool.ntp.org'),
            "mqtt": {
                "server": request.form.get('mqtt_server', ''),
                "port": int(request.form.get('mqtt_port', 8883)),
                "user": request.form.get('mqtt_user', ''),
                "password": request.form.get('mqtt_password', ''),
                "ssl": request.form.get('mqtt_ssl') == 'true',
                "department": request.form.get('mqtt_department', ''),
                "room": request.form.get('mqtt_room', ''),
                "id": request.form.get('mqtt_id', '')
            }
        }

        with open(SETTINGS_FILE, 'w') as f:
            json.dump(settings, f)

        content = "<h1>✅ Nastavenia uložené</h1><p><a href='/setup'>Späť na nastavenia</a></p>"
        return render_template("Uložené", content, "Nastavenia boli úspešne uložené!")

    except Exception as e:
        content = f"<h1>❌ Chyba</h1><p>{e}</p><p><a href='/setup'>Späť</a></p>"
        return render_template("Chyba", content, str(e), True)


@app.route('/restart', methods=['POST'])
@auth
async def restart(request):
    """Reštart zariadenia"""
    content = "<h1>🔄 Reštartujem...</h1><p>Zariadenie sa reštartuje...</p>"

    # Pošli odpoveď pred reštartom
    import uasyncio as asyncio
    asyncio.create_task(_delayed_reset())

    return render_template("Reštart", content)


async def _delayed_reset():
    """Oneskorený reštart"""
    import uasyncio as asyncio
    await asyncio.sleep(1)
    machine.reset()


@app.route('/login')
async def login(request):
    """Login stránka (info)"""
    content = """
    <h1>🔐 Prihlásenie</h1>
    <p>Pre prístup k nastaveniam použite Basic Authentication.</p>
    <p>Prehliadač vás vyzve na zadanie mena a hesla.</p>
    <p><a href="/setup">Prejsť na nastavenia</a></p>
    """
    return render_template("Prihlásenie", content)


def start_server(device, port=80):
    """Spusti web server"""
    global _device
    _device = device
    print(f"Web server na http://192.168.4.1:{port}")
    return app


def run_server(device, port=80):
    """Spusti server asynchrónne"""
    global _device
    _device = device
    print(f"Web server štartuje na porte {port}...")
    app.run(port=port)