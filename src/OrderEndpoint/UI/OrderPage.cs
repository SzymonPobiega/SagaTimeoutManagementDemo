namespace OrderEndpoint.UI;

static class OrderPage
{
    // User input only reaches the DOM through textContent and the URL through encodeURIComponent
    public const string Html = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Saga timeout demo</title>
        <style>
        body { font-family: sans-serif; max-width: 60rem; margin: 2rem auto; }
        td, th { text-align: left; padding: 0.2rem 0.8rem 0.2rem 0; }
        #status.error { color: #b00020; }
        </style>
        </head>
        <body>
        <h1>Saga timeout demo</h1>
        <form id="form">
        <label for="orderId">Order id</label>
        <input id="orderId" required maxlength="200">
        <button type="button" data-path="">Start order</button>
        <button type="button" data-path="/postpone">Postpone 1 minute</button>
        <button type="button" data-path="/expedite">Expedite 1 minute</button>
        </form>
        <p id="status" role="status" aria-live="polite" hidden></p>
        <h2>Recent history</h2>
        <p id="history-status" role="status" aria-live="polite">Loading history...</p>
        <table>
        <thead><tr><th>Time (UTC)</th><th>Order</th><th>Event</th><th>Detail</th></tr></thead>
        <tbody id="history"></tbody>
        </table>
        <script>
        const input = document.getElementById('orderId');
        const status = document.getElementById('status');
        const buttons = document.querySelectorAll('button');
        const historyStatus = document.getElementById('history-status');

        document.getElementById('form').addEventListener('submit', event => {
          event.preventDefault();
          if (!buttons[0].disabled) buttons[0].click();
        });

        function setStatus(text, isError) {
          status.textContent = text;
          status.className = isError ? 'error' : '';
          status.hidden = !text;
        }

        buttons.forEach(button => button.addEventListener('click', async () => {
          const orderId = input.value.trim();
          if (!orderId) {
            setStatus('Enter an order id.', true);
            input.focus();
            return;
          }
          buttons.forEach(b => b.disabled = true);
          try {
            const response = await fetch('/orders/' + encodeURIComponent(orderId) + button.dataset.path, { method: 'POST' });
            if (response.status === 202) {
              setStatus('', false);
            } else {
              setStatus('Rejected with HTTP ' + response.status + '.', true);
            }
          } catch (error) {
            setStatus('Request failed: ' + error.message, true);
          } finally {
            buttons.forEach(b => b.disabled = false);
          }
          refresh();
        }));

        async function refresh() {
          try {
            const response = await fetch('/history');
            if (!response.ok) throw new Error('HTTP ' + response.status);
            const rows = (await response.json()).map(entry => {
              const tr = document.createElement('tr');
              [new Date(entry.time).toISOString(), entry.orderId, entry.event, entry.detail].forEach(value => {
                const td = document.createElement('td');
                td.textContent = value;
                tr.appendChild(td);
              });
              return tr;
            });
            document.getElementById('history').replaceChildren(...rows);
            historyStatus.textContent = rows.length ? '' : 'No items processed yet.';
          } catch (error) {
            historyStatus.textContent = 'History unavailable; displayed entries may be stale. ' + error.message;
          }
        }

        refresh();
        setInterval(refresh, 2000);
        </script>
        </body>
        </html>
        """;
}
