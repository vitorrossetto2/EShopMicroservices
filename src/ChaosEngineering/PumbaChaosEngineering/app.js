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
            title: "Pumba API",
            version: "1.0.0",
            description: "API to trigger pumba chaos tests dynamically",
        },
    },
    apis: ["./app.js"], // Path to files with API documentation
};


/**
 * @swagger
 * /firstTest:
 *   get:
 *     summary: Run pumba test
 *     description: Executes the pumba script inside the container
 *     responses:
 *       200:
 *         description: Test executed successfully
 *       500:
 *         description: Error executing test
 */
app.get('/firstTest', (req, res) => {
    exec('pumba netem --interface src_default --duration 1m delay --time 1000 re2:src-basket.api-1', (error, stdout, stderr) => {
        if (error) {
            res.status(500).send(`Error: ${stderr}`);
        } else {
            res.send(`<pre>${stdout}</pre>`);
        }
    });
});


/**
 * @swagger
 * /secondTest:
 *   post:
 *     summary: Run pumba test
 *     description: Executes the pumba script inside the container
 *     requestBody:
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             properties:
 *               pumbaScript:
 *                 type: string
 *                 description: delay in ms
 *                 required: true
 *     responses:
 *       200:
 *         description: Test executed successfully
 *       500:
 *         description: Error executing test
 */
app.post('/secondTest', (req, res) => {
    //extract script from request body
    console.log(req.body);
    
    const script = req.body.pumbaScript;
    exec(`${script}`, (error, stdout, stderr) => {
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