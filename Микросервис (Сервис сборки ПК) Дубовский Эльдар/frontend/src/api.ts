const base = import.meta.env.VITE_API_URL ?? '';
export async function api<T>(path:string,method='GET',body?:unknown):Promise<T>{
  const token=sessionStorage.getItem('assembly-token');
  let response:Response;
  try{response=await fetch(base+path,{method,headers:{...(body===undefined?{}:{'Content-Type':'application/json'}),...(token?{Authorization:`Bearer ${token}`}:{})},body:body===undefined?undefined:JSON.stringify(body)});}catch{throw new Error('Нет соединения с сервисом сборки. Проверьте запуск API.');}
  if(!response.ok){const detail=await response.json().catch(()=>null);if(response.status===401){sessionStorage.removeItem('assembly-token');window.dispatchEvent(new Event('session-expired'));}throw new Error(detail?.message??`Ошибка запроса (${response.status}).`);}
  return response.status===204?undefined as T:await response.json() as T;
}
