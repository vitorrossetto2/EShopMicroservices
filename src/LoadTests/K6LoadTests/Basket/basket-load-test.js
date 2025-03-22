import http from 'k6/http';
import { sleep } from 'k6';

export default function () {
    var url = 'http://basket.api:8080/health';

    console.log('api ' + url)
    http.get(url);
    sleep(1);
}