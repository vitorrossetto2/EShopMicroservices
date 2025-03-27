const express = require('express');
const { exec } = require('child_process');
const app = express();
const swaggerUi = require('swagger-ui-express');
const swaggerJsdoc = require("swagger-jsdoc");

// Swagger Configuration
const swaggerOptions = {
    definition: {
        openapi: "3.0.0",
        info: {
            title: "K6 API",
            version: "1.0.0",
            description: "API to trigger k6 load tests dynamically",
        },
    },
    apis: ["./app.js"], // Path to files with API documentation
};


/**
 * @swagger
 * /basket/load:
 *   get:
 *     summary: Run k6 test
 *     description: Executes the k6 script inside the container
 *     responses:
 *       200:
 *         description: Test executed successfully
 *       500:
 *         description: Error executing test
 */
app.get('/basket/load', (req, res) => {
    exec('k6 run /app/Basket/basket-load-test.js', (error, stdout, stderr) => {
        if (error) {
            res.status(500).send(`Error: ${stderr}`);
        } else {
            res.send(`<pre>${stdout}</pre>`);
        }
    });
});



const swaggerSpec = swaggerJsdoc(swaggerOptions);
app.use("/swagger", swaggerUi.serve, swaggerUi.setup(swaggerSpec));

app.listen(3001, () => console.log('API running on port 3001'));